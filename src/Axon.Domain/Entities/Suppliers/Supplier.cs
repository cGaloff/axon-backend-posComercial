using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

// Catálogo fijo (no un TaxType-style catalog editable): a diferencia de los
// impuestos, el tipo de documento de un proveedor es una clasificación legal
// estable que no varía por tenant, así que un enum es consistente con cómo el
// resto del proyecto modela clasificaciones fijas (PaymentMethod, SaleStatus,
// CashMovementType, etc.) en vez de una tabla editable como TaxType.
public enum SupplierDocumentType
{
    NIT,
    CC,
    CE,
    Pasaporte
}

public class Supplier
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SupplierDocumentType DocumentType { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public string ContactName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Supplier()
    {
    }

    public static Supplier Create(
        string name,
        SupplierDocumentType documentType,
        string documentNumber,
        string contactName,
        string phone,
        string email,
        string? address = null,
        string? city = null)
    {
        ValidateRequiredFields(name, documentNumber, contactName, phone, email);

        return new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name,
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            ContactName = contactName,
            Phone = phone,
            Email = email,
            Address = address,
            City = city,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        SupplierDocumentType documentType,
        string documentNumber,
        string contactName,
        string phone,
        string email,
        string? address,
        string? city)
    {
        ValidateRequiredFields(name, documentNumber, contactName, phone, email);

        Name = name;
        DocumentType = documentType;
        DocumentNumber = documentNumber;
        ContactName = contactName;
        Phone = phone;
        Email = email;
        Address = address;
        City = city;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static void ValidateRequiredFields(
        string name, string documentNumber, string contactName, string phone, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("La razón social o nombre del proveedor es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            throw new DomainException("El número de documento del proveedor es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(contactName))
        {
            throw new DomainException("El nombre completo de contacto es obligatorio.");
        }

        // "Nombre completo con apellido": se exige al menos dos palabras (nombre y
        // apellido). No se valida gramática real de nombres propios, solo la forma.
        var nameParts = contactName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length < 2)
        {
            throw new DomainException("El nombre de contacto debe incluir nombre y apellido.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainException("El teléfono del proveedor es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("El correo electrónico del proveedor es obligatorio.");
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            throw new DomainException("El correo electrónico del proveedor no es válido.");
        }
    }
}
