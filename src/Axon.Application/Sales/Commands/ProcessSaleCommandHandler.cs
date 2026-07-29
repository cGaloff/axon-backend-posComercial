using Axon.Application.Interfaces;
using Axon.Application.Invoicing.Commands;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales.Commands;

public class ProcessSaleCommandHandler : IRequestHandler<ProcessSaleCommand, ProcessSaleResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IEmailService _emailService;
    private readonly IMediator _mediator;

    public ProcessSaleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICashSessionRepository cashSessionRepository,
        ICurrentUserContext currentUserContext,
        IEmailService emailService,
        IMediator mediator)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
        _currentUserContext = currentUserContext;
        _emailService = emailService;
        _mediator = mediator;
    }

    public async Task<ProcessSaleResult> Handle(ProcessSaleCommand request, CancellationToken cancellationToken)
    {
        // Se verifica ANTES de tocar productos/stock, y la sesión se persiste
        // en la misma transacción que la venta (un solo CommitAsync al final).
        var cashSession = await _cashSessionRepository.GetActiveSessionAsync(request.CashRegisterId);

        if (cashSession is null)
        {
            throw new DomainException("No hay una sesión de caja abierta. Abra la caja antes de procesar ventas.");
        }

        var createdBy = _currentUserContext.UserId;

        var productIds = request.Items.Select(i => i.ProductId).ToList();

        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(cancellationToken);

        var productsById = products.ToDictionary(p => p.Id);

        var missingProductId = productIds.FirstOrDefault(id => !productsById.ContainsKey(id));
        if (missingProductId != Guid.Empty)
        {
            throw new DomainException($"El producto '{missingProductId}' no existe o está inactivo");
        }

        // Se agrupa por producto antes de validar: si el mismo producto aparece en más
        // de una línea (p. ej. se escaneó el mismo código de barras varias veces en vez
        // de ajustar la cantidad), el stock disponible debe alcanzar para la suma de
        // todas las líneas, no solo para cada línea evaluada de forma aislada.
        var requestedQuantityByProduct = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        foreach (var (productId, requestedQuantity) in requestedQuantityByProduct)
        {
            var product = productsById[productId];

            if (product.Stock < requestedQuantity)
            {
                throw new DomainException(
                    $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}, solicitado: {requestedQuantity}");
            }
        }

        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(w => w.IsDefault, cancellationToken);

        if (warehouse is null)
        {
            throw new DomainException("No hay una bodega por defecto configurada");
        }

        // Catálogo completo de impuestos del tenant, para resolver el nombre que se
        // snapshotea en cada línea de venta junto al porcentaje vigente del producto.
        var taxTypeNames = await _dbContext.TaxTypes.ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var sale = Sale.Create(
            request.CashRegisterId,
            createdBy,
            request.CustomerId,
            request.CustomerName,
            request.Notes,
            request.CustomerDocumentType,
            request.CustomerDocumentNumber);

        var movements = new List<InventoryMovement>();
        var alerts = new List<StockAlert>();

        // Descuento manual por producto: monto fijo (Discount) o % (DiscountPercentage,
        // convertido aquí al monto equivalente sobre el precio de ESTE producto). Se
        // resuelve antes de todo lo demás porque el reparto del descuento general (más
        // abajo) necesita el monto ya concreto de cada línea, no importa cómo se dio.
        var itemDiscountAmounts = request.Items
            .Select(item =>
            {
                if (item.Discount.HasValue)
                {
                    return item.Discount.Value;
                }

                if (item.DiscountPercentage.HasValue)
                {
                    var grossSubtotal = productsById[item.ProductId].Price * item.Quantity;
                    return grossSubtotal * item.DiscountPercentage.Value / 100;
                }

                return 0m;
            })
            .ToList();

        // Descuento general de la venta: se reparte proporcionalmente al subtotal
        // de cada línea YA con su descuento manual descontado (preGeneralSubtotal),
        // así que si un ítem ya trae un descuento manual grande, recibe una porción
        // menor del descuento general (no se "descuenta dos veces" sobre lo mismo).
        // El residuo de redondeo se asigna a la última línea para que la suma de
        // las porciones cuadre exacto con el monto total.
        var preGeneralSubtotals = request.Items
            .Select((item, i) => productsById[item.ProductId].Price * item.Quantity - itemDiscountAmounts[i])
            .ToList();

        var totalPreGeneralSubtotal = preGeneralSubtotals.Sum();

        decimal generalDiscountAmount;

        if (request.SaleDiscountAmount.HasValue)
        {
            generalDiscountAmount = request.SaleDiscountAmount.Value;
        }
        else if (request.SaleDiscountPercentage.HasValue)
        {
            generalDiscountAmount = totalPreGeneralSubtotal * request.SaleDiscountPercentage.Value / 100;
        }
        else
        {
            generalDiscountAmount = 0;
        }

        if (generalDiscountAmount > totalPreGeneralSubtotal)
        {
            throw new DomainException("El descuento general no puede ser mayor al subtotal de la venta.");
        }

        // Mismo tope de descuento por rol que ya aplica por producto (ver más
        // abajo), pero evaluado sobre el % efectivo del descuento general.
        if (_currentUserContext.MaxDiscountPercentage.HasValue && generalDiscountAmount > 0)
        {
            var generalDiscountPercentage = totalPreGeneralSubtotal > 0
                ? generalDiscountAmount / totalPreGeneralSubtotal * 100
                : 0m;

            if (generalDiscountPercentage > _currentUserContext.MaxDiscountPercentage.Value)
            {
                throw new DomainException(
                    $"El descuento general de {generalDiscountPercentage:0.##}% supera el tope permitido para su rol ({_currentUserContext.MaxDiscountPercentage.Value:0.##}%). Se requiere autorización de un rol con mayor tope.");
            }
        }

        var generalDiscountShares = new decimal[request.Items.Count];
        var allocatedGeneralDiscount = 0m;

        for (var i = 0; i < request.Items.Count; i++)
        {
            if (i == request.Items.Count - 1)
            {
                generalDiscountShares[i] = generalDiscountAmount - allocatedGeneralDiscount;
                continue;
            }

            var share = totalPreGeneralSubtotal > 0
                ? generalDiscountAmount * (preGeneralSubtotals[i] / totalPreGeneralSubtotal)
                : 0m;

            generalDiscountShares[i] = share;
            allocatedGeneralDiscount += share;
        }

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var product = productsById[item.ProductId];
            var itemDiscountAmount = itemDiscountAmounts[i];

            // Tope de descuento por rol (Matriz de Roles y Permisos v2, regla
            // transversal C): null = sin tope (Administrador/Propietario). Se valida
            // aquí, en Application, porque depende de QUIÉN hace la venta, no es un
            // invariante propio de SaleItem (el mismo descuento puede ser válido para
            // un Administrador e inválido para un Cajero).
            if (_currentUserContext.MaxDiscountPercentage.HasValue && itemDiscountAmount > 0)
            {
                var grossSubtotal = product.Price * item.Quantity;
                var discountPercentage = grossSubtotal > 0 ? itemDiscountAmount / grossSubtotal * 100 : 0m;

                if (discountPercentage > _currentUserContext.MaxDiscountPercentage.Value)
                {
                    throw new DomainException(
                        $"El descuento de {discountPercentage:0.##}% en '{product.Name}' supera el tope permitido para su rol ({_currentUserContext.MaxDiscountPercentage.Value:0.##}%). Se requiere autorización de un rol con mayor tope.");
                }
            }

            // Snapshot de los impuestos vigentes del producto (0 a N), tal como están
            // configurados en este instante. TenantConfig.IsResponsableIva ya no filtra
            // impuestos automáticamente aquí: en el modelo flexible, si un tenant no
            // responsable de IVA no quiere cobrarlo, simplemente no debe asignar ese
            // TaxType a sus productos (ver resumen de diseño del prompt 3).
            var appliedTaxes = product.Taxes
                .Select(pt => (pt.TaxTypeId, taxTypeNames.GetValueOrDefault(pt.TaxTypeId, string.Empty), pt.Percentage))
                .ToList();

            var saleItem = SaleItem.Create(
                saleId: sale.Id,
                productId: product.Id,
                productName: product.Name,
                productSku: product.Sku,
                unitPrice: product.Price,
                quantity: item.Quantity,
                discount: itemDiscountAmount,
                discountPercentage: item.DiscountPercentage,
                generalDiscountShare: generalDiscountShares[i],
                unitCost: product.Cost,
                appliedTaxes: appliedTaxes);

            sale.AddItem(saleItem);

            var stockBefore = product.Stock;
            product.AdjustStock(-item.Quantity);

            movements.Add(InventoryMovement.Create(
                product.Id,
                warehouse.Id,
                InventoryMovementType.Sale,
                -item.Quantity,
                stockBefore,
                $"Venta {sale.SaleNumber}",
                createdBy));

            if (product.Stock <= product.MinStock)
            {
                alerts.Add(StockAlert.Create(product.Id, warehouse.Id, product.Stock, product.MinStock));
            }
        }

        sale.SetGeneralDiscountAmount(generalDiscountAmount, request.SaleDiscountPercentage);

        var cashMovements = new List<CashMovement>();

        foreach (var paymentRequest in request.Payments)
        {
            var payment = SalePayment.Create(sale.Id, paymentRequest.Method, paymentRequest.Amount, paymentRequest.AmountTendered);
            sale.AddPayment(payment);

            var movementType = MapToCashMovementType(payment.Method);

            cashMovements.Add(CashMovement.Create(
                cashSession.Id,
                movementType,
                payment.Amount,
                $"Venta {sale.SaleNumber}",
                createdBy,
                sale.Id));

            // Solo Cash y Credit modifican el monto físico esperado en caja;
            // Card y Transfer quedan registrados como movimiento informativo.
            cashSession.AddCashMovement(payment.Amount, movementType);
        }

        // La suma de los pagos debe cubrir el total dentro de la tolerancia de
        // redondeo definida en Sale.PaymentTolerance (ver comentario en la entidad).
        sale.EnsurePaymentsMatchTotal();

        _dbContext.Sales.Add(sale);
        _dbContext.InventoryMovements.AddRange(movements);
        _dbContext.StockAlerts.AddRange(alerts);
        _dbContext.CashMovements.AddRange(cashMovements);
        _cashSessionRepository.Update(cashSession);

        await _unitOfWork.CommitAsync(cancellationToken);

        // Toda venta presencial queda Completed de inmediato sin importar el método de
        // pago (ver Sale.AddPayment), así que la factura siempre se emite aquí mismo.
        byte[]? pdf = null;
        long? invoiceNumber = null;

        if (sale.Status == SaleStatus.Completed)
        {
            var issueResult = await _mediator.Send(new IssueInvoiceCommand(sale.Id), cancellationToken);
            pdf = issueResult.PdfReceipt;
            invoiceNumber = issueResult.Number;

            if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
            {
                // Fire-and-forget: el correo no debe bloquear la respuesta al cajero.
                _ = _emailService.SendSaleReceiptAsync(request.CustomerEmail, request.CustomerName ?? string.Empty, sale, pdf);
            }
        }

        var totalChange = sale.Payments.Sum(p => p.Change ?? 0m);

        return new ProcessSaleResult(sale.Id, sale.SaleNumber, sale.Total, totalChange, sale.Status, invoiceNumber, pdf);
    }

    private static CashMovementType MapToCashMovementType(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => CashMovementType.CashSale,
        PaymentMethod.Credit => CashMovementType.CreditSale,
        PaymentMethod.Card => CashMovementType.CardSale,
        PaymentMethod.Transfer => CashMovementType.TransferSale,
        _ => throw new DomainException("Método de pago no soportado")
    };
}
