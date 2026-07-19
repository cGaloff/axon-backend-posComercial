using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Suppliers;

public class Supplier
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Nit { get; private set; }
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public int PaymentTermDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Supplier()
    {
    }

    public static Supplier Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del proveedor es obligatorio.");
        }

        return new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name,
            PaymentTermDays = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string? nit,
        string? contactName,
        string? phone,
        string? email,
        string? address,
        string? city,
        int paymentTermDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del proveedor es obligatorio.");
        }

        Name = name;
        Nit = nit;
        ContactName = contactName;
        Phone = phone;
        Email = email;
        Address = address;
        City = city;
        PaymentTermDays = paymentTermDays;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
