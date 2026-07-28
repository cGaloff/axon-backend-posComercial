using Axon.Application.Inventory.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Taxes;

namespace Axon.Application.Tests.Inventory;

// El frontend debe poder mostrar la descripción larga de cada impuesto y
// enviar de vuelta su Code (el enum fijo, TaxCode) además del Id — ver
// GetTaxTypesQuery.TaxTypeDto.
public class GetTaxTypesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCodeAndDescriptionForEachTaxType()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        dbContext.TaxTypes.Add(iva);
        await dbContext.SaveChangesAsync();

        var handler = new GetTaxTypesQueryHandler(dbContext);

        var result = await handler.Handle(new GetTaxTypesQuery(), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(iva.Id, dto.Id);
        Assert.Equal(TaxCode.Iva, dto.Code);
        Assert.Equal("IVA", dto.Name);
        Assert.Equal("Impuesto sobre las ventas", dto.Description);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task Handle_ByDefault_ExcludesDeactivatedTaxTypes()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var iva = TaxType.Create(TaxCode.Iva, "IVA", "Impuesto sobre las ventas");
        var gmf = TaxType.Create(TaxCode.Gmf, "GMF", "Gravamen a los movimientos financieros");
        gmf.Deactivate();

        dbContext.TaxTypes.AddRange(iva, gmf);
        await dbContext.SaveChangesAsync();

        var handler = new GetTaxTypesQueryHandler(dbContext);

        var result = await handler.Handle(new GetTaxTypesQuery(IncludeInactive: false), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(TaxCode.Iva, dto.Code);
    }
}
