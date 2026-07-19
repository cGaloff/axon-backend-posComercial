using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

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

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromAddress));
            message.To.Add(new MailboxAddress(customerName, toEmail));
            message.Subject = $"Recibo de compra - {sale.SaleNumber}";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hola {customerName}, adjuntamos el recibo de tu compra {sale.SaleNumber}."
            };
            bodyBuilder.Attachments.Add($"{sale.SaleNumber}.pdf", pdfBytes, new ContentType("application", "pdf"));
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.Auto);

            if (!string.IsNullOrEmpty(username))
            {
                await client.AuthenticateAsync(username, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el recibo de la venta {SaleNumber} a {Email}", sale.SaleNumber, toEmail);
        }
    }
}
