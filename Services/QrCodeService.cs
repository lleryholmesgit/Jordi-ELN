using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace ElectronicLabNotebook.Services;

public sealed class QrCodeService : IQrCodeService
{
    private readonly byte[] _key;

    public QrCodeService(IConfiguration configuration)
    {
        var signingKey = configuration["QrCode:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("QrCode:SigningKey is required and must be provided through secure configuration.");
        }

        _key = Encoding.UTF8.GetBytes(signingKey);
    }

    public string GenerateToken(string instrumentCode)
    {
        return GenerateScopedToken("instrument", instrumentCode);
    }

    public bool TryParseToken(string token, out string instrumentCode)
    {
        return TryParseScopedToken(token, "instrument", out instrumentCode);
    }

    public string GenerateStorageLocationToken(string storageLocationCode)
    {
        return GenerateScopedToken("storage-location", storageLocationCode);
    }

    public bool TryParseStorageLocationToken(string token, out string storageLocationCode)
    {
        return TryParseScopedToken(token, "storage-location", out storageLocationCode);
    }

    public string GenerateSvg(string token)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(data);
        return qrCode.GetGraphic(8);
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_key);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes[..8]);
    }

    private string GenerateScopedToken(string scope, string code)
    {
        var payload = $"{scope}:{code}";
        var signature = ComputeSignature(payload);
        return $"eln://{scope}/{code}?sig={signature}";
    }

    private bool TryParseScopedToken(string token, string scope, out string code)
    {
        code = string.Empty;
        var prefix = $"eln://{scope}/";
        if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = token[prefix.Length..].Split("?sig=", StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var parsedCode = parts[0];
        var signature = parts[1];
        if (!string.Equals(signature, ComputeSignature($"{scope}:{parsedCode}"), StringComparison.Ordinal))
        {
            return false;
        }

        code = parsedCode;
        return true;
    }
}
