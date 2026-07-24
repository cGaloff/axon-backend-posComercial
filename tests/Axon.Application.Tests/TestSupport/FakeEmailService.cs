using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeEmailService : IEmailService
{
    public Task SendSaleReceiptAsync(string toEmail, string customerName, Sale sale, byte[] pdfBytes) => Task.CompletedTask;
}
