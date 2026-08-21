using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeEmailService : IEmailService
{
    public string? LastPasswordResetEmail { get; private set; }
    public string? LastPasswordResetLink { get; private set; }

    public Task SendSaleReceiptAsync(string toEmail, string customerName, Sale sale, byte[] pdfBytes) => Task.CompletedTask;

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink)
    {
        LastPasswordResetEmail = toEmail;
        LastPasswordResetLink = resetLink;
        return Task.CompletedTask;
    }

    public string? LastVerificationEmail { get; private set; }
    public string? LastVerificationCode { get; private set; }

    public Task SendRegistrationVerificationCodeAsync(string toEmail, string businessName, string code)
    {
        LastVerificationEmail = toEmail;
        LastVerificationCode = code;
        return Task.CompletedTask;
    }

    public List<(string Email, int DaysRemaining)> TrialReminders { get; } = new();
    public List<string> TrialExpiredEmails { get; } = new();

    public Task SendTrialReminderAsync(string toEmail, string businessName, int daysRemaining)
    {
        TrialReminders.Add((toEmail, daysRemaining));
        return Task.CompletedTask;
    }

    public Task SendTrialExpiredAsync(string toEmail, string businessName)
    {
        TrialExpiredEmails.Add(toEmail);
        return Task.CompletedTask;
    }
}
