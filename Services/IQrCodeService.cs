namespace ElectronicLabNotebook.Services;

public interface IQrCodeService
{
    string GenerateToken(string instrumentCode);
    bool TryParseToken(string token, out string instrumentCode);
    string GenerateStorageLocationToken(string storageLocationCode);
    bool TryParseStorageLocationToken(string token, out string storageLocationCode);
    string GenerateSvg(string token);
}
