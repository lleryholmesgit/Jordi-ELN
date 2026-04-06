namespace ElectronicLabNotebook.Services;

public interface IQrCodeService
{
    string GenerateToken(string instrumentCode);
    bool TryParseToken(string token, out string instrumentCode);
    string GenerateSvg(string token);
}