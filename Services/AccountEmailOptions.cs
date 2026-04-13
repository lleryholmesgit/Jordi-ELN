namespace ElectronicLabNotebook.Services;

public sealed class AccountEmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "Jordi ELN";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PickupDirectory { get; set; } = "App_Data/OutgoingEmail";
}
