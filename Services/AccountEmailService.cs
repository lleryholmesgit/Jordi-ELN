using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ElectronicLabNotebook.Services;

public sealed class AccountEmailService : IAccountEmailService
{
    private readonly AccountEmailOptions _options;
    private readonly IWebHostEnvironment _environment;

    public AccountEmailService(IOptions<AccountEmailOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<string> SendPasswordResetEmailAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        var subject = "Jordi ELN password reset";
        var plainTextBody = $"""
Please use the link below to reset your Jordi ELN password:

{resetUrl}

If you did not request this change, contact your Jordi ELN administrator.
""";

        if (!string.IsNullOrWhiteSpace(_options.SmtpHost) && !string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
                Subject = subject,
                Body = plainTextBody
            };
            message.To.Add(recipientEmail);

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            return $"Password reset email sent to {recipientEmail}.";
        }

        var pickupDirectory = Path.IsPathRooted(_options.PickupDirectory)
            ? _options.PickupDirectory
            : Path.Combine(_environment.ContentRootPath, _options.PickupDirectory);

        Directory.CreateDirectory(pickupDirectory);

        var safeEmail = string.Join("_", recipientEmail.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var filePath = Path.Combine(pickupDirectory, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{safeEmail}-password-reset.txt");
        await File.WriteAllTextAsync(filePath, $"To: {recipientEmail}{Environment.NewLine}Subject: {subject}{Environment.NewLine}{Environment.NewLine}{plainTextBody}", cancellationToken);

        return $"SMTP is not configured. A password reset email draft was written to {filePath}.";
    }
}
