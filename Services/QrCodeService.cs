using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace ElectronicLabNotebook.Services;

public sealed class QrCodeService : IQrCodeService
{
    private readonly byte[] _key;

    public QrCodeService(IConfiguration configuration)
    {
        var signingKey = configuration["QrCode:SigningKey"] ?? throw new InvalidOperationException("QrCode signing key is missing.");
        _key = Encoding.UTF8.GetBytes(signingKey);
    }

    public string GenerateToken(string instrumentCode)
    {
        var signature = ComputeSignature(instrumentCode);
        return $"eln://instrument/{instrumentCode}?sig={signature}";
    }

    public bool TryParseToken(string token, out string instrumentCode)
    {
        instrumentCode = string.Empty;
        const string prefix = "eln://instrument/";
        if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = token[prefix.Length..].Split("?sig=", StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var code = parts[0];
        var signature = parts[1];
        if (!string.Equals(signature, ComputeSignature(code), StringComparison.Ordinal))
        {
            return false;
        }

        instrumentCode = code;
        return true;
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
}