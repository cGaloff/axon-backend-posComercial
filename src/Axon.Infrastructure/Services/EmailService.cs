using System.Net;
using System.Net.Mail;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Axon.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendSaleReceiptAsync(string toEmail, string customerName, Sale sale, byte[] pdfBytes)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = _configuration.GetValue<int>("Email:SmtpPort");
            var fromAddress = _configuration["Email:FromAddress"] ?? string.Empty;
            var fromName = _configuration["Email:FromName"];
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = $"Recibo de compra - {sale.SaleNumber}",
                Body = $"Hola {customerName}, adjuntamos el recibo de tu compra {sale.SaleNumber}.",
                IsBodyHtml = false
            };

            message.To.Add(toEmail);

            using var attachmentStream = new MemoryStream(pdfBytes);
            message.Attachments.Add(new Attachment(attachmentStream, $"{sale.SaleNumber}.pdf", "application/pdf"));

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el recibo de la venta {SaleNumber} a {Email}", sale.SaleNumber, toEmail);
        }
    }
}
