using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Interfaces;

public interface IEmailService
{
    Task SendSaleReceiptAsync(string toEmail, string customerName, Sale sale, byte[] pdfBytes);
}
