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
            request.Notes);

        var movements = new List<InventoryMovement>();
        var alerts = new List<StockAlert>();

        foreach (var item in request.Items)
        {
            var product = productsById[item.ProductId];

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
                discount: item.Discount,
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

        // La factura (PDF + registro Invoice) solo se emite cuando la venta queda
        // Completed de inmediato (efectivo/fiado). Si algún pago requiere
        // confirmación externa (tarjeta/transferencia), la venta queda
        // PendingPayment y la factura se emite después, desde
        // ConfirmSalePaymentCommandHandler — mismo evento ("pago exitoso"), dos
        // puntos de entrada posibles.
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
