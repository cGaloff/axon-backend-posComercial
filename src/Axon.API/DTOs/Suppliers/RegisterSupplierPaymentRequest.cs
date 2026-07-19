namespace Axon.API.DTOs.Suppliers;

public class RegisterSupplierPaymentRequest
{
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
