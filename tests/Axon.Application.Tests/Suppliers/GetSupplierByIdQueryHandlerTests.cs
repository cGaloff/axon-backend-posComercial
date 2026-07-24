using Axon.Application.Suppliers.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Exceptions;

namespace Axon.Application.Tests.Suppliers;

public class GetSupplierByIdQueryHandlerTests
{
    // Al seleccionar un proveedor en el formulario de compra, la respuesta debe
    // traer toda su información para autocompletar en frontend (sin obligar a
    // reingresar datos que el proveedor ya tiene registrados).
    [Fact]
    public async Task Handle_ReturnsFullSupplierInfoForAutocomplete()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var supplier = Supplier.Create(
            name: "Distribuidora ACME S.A.S.",
            documentType: SupplierDocumentType.NIT,
            documentNumber: "900123456-7",
            contactName: "Juan Perez",
            phone: "3001234567",
            email: "juan.perez@acme.com",
            address: "Calle 123 #45-67",
            city: "Bogotá");

        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var handler = new GetSupplierByIdQueryHandler(dbContext);

        var result = await handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

        Assert.Equal(supplier.Name, result.Name);
        Assert.Equal(supplier.DocumentType, result.DocumentType);
        Assert.Equal(supplier.DocumentNumber, result.DocumentNumber);
        Assert.Equal(supplier.ContactName, result.ContactName);
        Assert.Equal(supplier.Phone, result.Phone);
        Assert.Equal(supplier.Email, result.Email);
        Assert.Equal(supplier.Address, result.Address);
        Assert.Equal(supplier.City, result.City);
    }

    [Fact]
    public async Task Handle_WhenSupplierDoesNotExist_Throws()
    {
        await using var dbContext = TestDbContextFactory.Create();

        var handler = new GetSupplierByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
