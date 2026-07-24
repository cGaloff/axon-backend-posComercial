using Axon.Domain.Entities.Suppliers;

namespace Axon.API.DTOs.Suppliers;

public class CreateSupplierRequest
{
    public string Name { get; set; } = default!;
    public SupplierDocumentType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = default!;
    public string ContactName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Address { get; set; }
    public string? City { get; set; }
}
