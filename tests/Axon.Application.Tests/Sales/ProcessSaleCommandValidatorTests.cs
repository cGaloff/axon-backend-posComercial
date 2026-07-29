using Axon.Application.Sales.Commands;
using Axon.Domain.Entities.Sales;

namespace Axon.Application.Tests.Sales;

public class ProcessSaleCommandValidatorTests
{
    private static readonly ProcessSaleCommandValidator Validator = new();

    private static ProcessSaleCommand BaseCommand(decimal? saleDiscountAmount = null, decimal? saleDiscountPercentage = null) =>
        new(
            Items: new List<SaleItemRequest> { new(Guid.NewGuid(), Quantity: 1) },
            Payments: new List<SalePaymentRequest> { new(PaymentMethod.Cash, 100m) },
            CashRegisterId: Guid.NewGuid(),
            CustomerId: null,
            CustomerName: null,
            CustomerEmail: null,
            Notes: null,
            SaleDiscountAmount: saleDiscountAmount,
            SaleDiscountPercentage: saleDiscountPercentage);

    [Fact]
    public void Validate_WithNeitherSaleDiscountField_IsValid()
    {
        var result = Validator.Validate(BaseCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithOnlySaleDiscountAmount_IsValid()
    {
        var result = Validator.Validate(BaseCommand(saleDiscountAmount: 100m));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithOnlySaleDiscountPercentage_IsValid()
    {
        var result = Validator.Validate(BaseCommand(saleDiscountPercentage: 10m));

        Assert.True(result.IsValid);
    }

    // Monto y porcentaje son mutuamente excluyentes: si el cajero da los dos,
    // el handler no sabría cuál usar.
    [Fact]
    public void Validate_WithBothSaleDiscountAmountAndPercentage_IsInvalid()
    {
        var result = Validator.Validate(BaseCommand(saleDiscountAmount: 100m, saleDiscountPercentage: 10m));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithNegativeSaleDiscountAmount_IsInvalid()
    {
        var result = Validator.Validate(BaseCommand(saleDiscountAmount: -1m));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithSaleDiscountPercentageOver100_IsInvalid()
    {
        var result = Validator.Validate(BaseCommand(saleDiscountPercentage: 101m));

        Assert.False(result.IsValid);
    }

    // Igual que el descuento general, pero a nivel de cada producto: monto o %,
    // nunca ambos para la misma línea.
    [Fact]
    public void Validate_WithItemDiscountAmount_IsValid()
    {
        var command = BaseCommand() with
        {
            Items = new List<SaleItemRequest> { new(Guid.NewGuid(), Quantity: 1, Discount: 5000m) }
        };

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithItemDiscountPercentage_IsValid()
    {
        var command = BaseCommand() with
        {
            Items = new List<SaleItemRequest> { new(Guid.NewGuid(), Quantity: 1, DiscountPercentage: 5m) }
        };

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithBothItemDiscountAndPercentage_IsInvalid()
    {
        var command = BaseCommand() with
        {
            Items = new List<SaleItemRequest> { new(Guid.NewGuid(), Quantity: 1, Discount: 5000m, DiscountPercentage: 5m) }
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithItemDiscountPercentageOver100_IsInvalid()
    {
        var command = BaseCommand() with
        {
            Items = new List<SaleItemRequest> { new(Guid.NewGuid(), Quantity: 1, DiscountPercentage: 150m) }
        };

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }
}
