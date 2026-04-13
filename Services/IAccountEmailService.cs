namespace ElectronicLabNotebook.Services;

public interface IAccountEmailService
{
    Task<string> SendPasswordResetEmailAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default);
}
