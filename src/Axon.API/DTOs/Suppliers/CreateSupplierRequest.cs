namespace Axon.API.DTOs.Suppliers;

public class CreateSupplierRequest
{
    public string Name { get; set; } = default!;
    public string? Nit { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public int PaymentTermDays { get; set; } = 30;
}
