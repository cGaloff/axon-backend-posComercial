using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Suppliers;

public class SupplierTests
{
    private static Supplier CreateValidSupplier()
    {
        return Supplier.Create(
            name: "Distribuidora ACME S.A.S.",
            documentType: SupplierDocumentType.NIT,
            documentNumber: "900123456-7",
            contactName: "Juan Perez",
            phone: "3001234567",
            email: "juan.perez@acme.com");
    }

    [Fact]
    public void Create_WithDocumentTypeAndValidNumber_Succeeds()
    {
        var supplier = CreateValidSupplier();

        Assert.Equal(SupplierDocumentType.NIT, supplier.DocumentType);
        Assert.Equal("900123456-7", supplier.DocumentNumber);
        Assert.True(supplier.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithoutName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => Supplier.Create(
            name, SupplierDocumentType.NIT, "900123456-7", "Juan Perez", "3001234567", "juan@acme.com"));
    }

    [Fact]
    public void Create_WithoutDocumentNumber_Throws()
    {
        Assert.Throws<DomainException>(() => Supplier.Create(
            "ACME", SupplierDocumentType.NIT, "", "Juan Perez", "3001234567", "juan@acme.com"));
    }

    [Fact]
    public void Create_WithoutPhone_Throws()
    {
        Assert.Throws<DomainException>(() => Supplier.Create(
            "ACME", SupplierDocumentType.NIT, "900123456-7", "Juan Perez", "", "juan@acme.com"));
    }

    [Fact]
    public void Create_WithoutEmail_Throws()
    {
        Assert.Throws<DomainException>(() => Supplier.Create(
            "ACME", SupplierDocumentType.NIT, "900123456-7", "Juan Perez", "3001234567", ""));
    }

    [Fact]
    public void Create_WithInvalidEmail_Throws()
    {
        Assert.Throws<DomainException>(() => Supplier.Create(
            "ACME", SupplierDocumentType.NIT, "900123456-7", "Juan Perez", "3001234567", "correo-invalido"));
    }

    // "Nombre completo con apellido": un solo nombre (sin apellido) debe rechazarse.
    [Fact]
    public void Create_WithContactNameMissingLastName_Throws()
    {
        Assert.Throws<DomainException>(() => Supplier.Create(
            "ACME", SupplierDocumentType.NIT, "900123456-7", "Juan", "3001234567", "juan@acme.com"));
    }

    [Fact]
    public void Create_WithDocumentTypeCC_Succeeds()
    {
        var supplier = Supplier.Create(
            "Persona Natural Proveedora", SupplierDocumentType.CC, "1020304050",
            "Maria Gomez", "3009876543", "maria@correo.com");

        Assert.Equal(SupplierDocumentType.CC, supplier.DocumentType);
    }
}
