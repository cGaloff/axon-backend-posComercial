using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Sales.Commands;

public class ProcessSaleCommandHandler : IRequestHandler<ProcessSaleCommand, ProcessSaleResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPdfService _pdfService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public ProcessSaleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICashSessionRepository cashSessionRepository,
        ICurrentUserContext currentUserContext,
        IPdfService pdfService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
        _currentUserContext = currentUserContext;
        _pdfService = pdfService;
        _emailService = emailService;
        _configuration = configuration;
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

        foreach (var item in request.Items)
        {
            var product = productsById[item.ProductId];

            if (product.Stock < item.Quantity)
            {
                throw new DomainException(
                    $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}");
            }
        }

        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(w => w.IsDefault, cancellationToken);

        if (warehouse is null)
        {
            throw new DomainException("No hay una bodega por defecto configurada");
        }

        var sale = Sale.Create(
            request.PaymentMethod,
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

            var saleItem = SaleItem.Create(
                sale.Id,
                product.Id,
                product.Name,
                product.Sku,
                product.Price,
                item.Quantity,
                item.Discount);

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

        if (request.PaymentMethod == PaymentMethod.Cash)
        {
            sale.SetAmountPaid(request.AmountPaid);
        }

        var movementType = request.PaymentMethod switch
        {
            PaymentMethod.Cash => CashMovementType.CashSale,
            PaymentMethod.Credit => CashMovementType.CreditSale,
            PaymentMethod.Card => CashMovementType.CardSale,
            PaymentMethod.Transfer => CashMovementType.TransferSale,
            _ => throw new DomainException("Método de pago no soportado")
        };

        var cashMovement = CashMovement.Create(
            cashSession.Id,
            movementType,
            sale.Total,
            $"Venta {sale.SaleNumber}",
            createdBy,
            sale.Id);

        // Solo Cash y Credit modifican el monto físico esperado en caja;
        // Card y Transfer quedan registrados como movimiento informativo.
        cashSession.AddCashMovement(sale.Total, movementType);

        _dbContext.Sales.Add(sale);
        _dbContext.InventoryMovements.AddRange(movements);
        _dbContext.StockAlerts.AddRange(alerts);
        _dbContext.CashMovements.Add(cashMovement);
        _cashSessionRepository.Update(cashSession);

        await _unitOfWork.CommitAsync(cancellationToken);

        // El PDF se genera después del commit para no mantener I/O dentro de la transacción.
        var businessName = _configuration["BusinessName"] ?? "Axon POS";
        var pdf = _pdfService.GenerateSaleReceipt(sale, businessName);

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            // Fire-and-forget: el correo no debe bloquear la respuesta al cajero.
            _ = _emailService.SendSaleReceiptAsync(request.CustomerEmail, request.CustomerName ?? string.Empty, sale, pdf);
        }

        return new ProcessSaleResult(sale.Id, sale.SaleNumber, sale.Total, sale.Change, sale.Status, pdf);
    }
}
