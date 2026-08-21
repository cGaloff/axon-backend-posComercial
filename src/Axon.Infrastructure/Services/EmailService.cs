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

    public async Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink)
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
            message.To.Add(new MailboxAddress(fullName, toEmail));
            message.Subject = "Recuperación de contraseña";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hola {fullName}, recibimos una solicitud para restablecer tu contraseña. " +
                           $"Si fuiste vos, usá este enlace (válido por tiempo limitado): {resetLink}\n\n" +
                           "Si no pediste esto, podés ignorar este correo."
            };
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
            _logger.LogError(ex, "No se pudo enviar el correo de recuperación de contraseña a {Email}", toEmail);
        }
    }

    public async Task SendRegistrationVerificationCodeAsync(string toEmail, string businessName, string code)
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
            message.To.Add(new MailboxAddress(businessName, toEmail));
            message.Subject = "Confirmá tu correo para activar tu cuenta";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hola, para activar la cuenta de {businessName} usá este código de verificación: {code}\n\n" +
                           "Es válido por tiempo limitado. Si no pediste este registro, podés ignorar este correo."
            };
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
            _logger.LogError(ex, "No se pudo enviar el código de verificación de registro a {Email}", toEmail);
        }
    }

    public async Task SendTrialReminderAsync(string toEmail, string businessName, int daysRemaining)
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
            message.To.Add(new MailboxAddress(businessName, toEmail));
            message.Subject = $"Te quedan {daysRemaining} días de prueba gratuita";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hola, la prueba gratuita de {businessName} vence en {daysRemaining} día(s). " +
                           "Regularizá el pago antes de esa fecha para que tu cuenta no quede bloqueada."
            };
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
            _logger.LogError(ex, "No se pudo enviar el recordatorio de vencimiento de prueba a {Email}", toEmail);
        }
    }

    public async Task SendTrialExpiredAsync(string toEmail, string businessName)
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
            message.To.Add(new MailboxAddress(businessName, toEmail));
            message.Subject = "Tu período de prueba venció";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hola, el período de prueba gratuita de {businessName} venció y tu cuenta quedó " +
                           "bloqueada hasta que se regularice el pago."
            };
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
            _logger.LogError(ex, "No se pudo enviar el aviso de vencimiento de prueba a {Email}", toEmail);
        }
    }
}
