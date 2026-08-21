using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Interfaces;

public interface IEmailService
{
    Task SendSaleReceiptAsync(string toEmail, string customerName, Sale sale, byte[] pdfBytes);

    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink);

    Task SendRegistrationVerificationCodeAsync(string toEmail, string businessName, string code);

    Task SendTrialReminderAsync(string toEmail, string businessName, int daysRemaining);

    Task SendTrialExpiredAsync(string toEmail, string businessName);
}
