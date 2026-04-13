using ElectronicLabNotebook.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ElectronicLabNotebook.Tests;

public sealed class QrCodeServiceTests
{
    [Fact]
    public void GenerateToken_ProducesRoundTrippablePayload()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QrCode:SigningKey"] = "unit-test-key"
            })
            .Build();

        var service = new QrCodeService(configuration);
        var token = service.GenerateToken("HPLC-001");

        var success = service.TryParseToken(token, out var code);

        Assert.True(success);
        Assert.Equal("HPLC-001", code);
    }

    [Fact]
    public void TryParseToken_RejectsTamperedPayload()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QrCode:SigningKey"] = "unit-test-key"
            })
            .Build();

        var service = new QrCodeService(configuration);
        var token = service.GenerateToken("HPLC-001").Replace("HPLC-001", "HPLC-999", StringComparison.Ordinal);

        var success = service.TryParseToken(token, out _);

        Assert.False(success);
    }

    [Fact]
    public void GenerateStorageLocationToken_ProducesRoundTrippablePayload()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QrCode:SigningKey"] = "unit-test-key"
            })
            .Build();

        var service = new QrCodeService(configuration);
        var token = service.GenerateStorageLocationToken("J-STO-ABCD1234");

        var success = service.TryParseStorageLocationToken(token, out var code);

        Assert.True(success);
        Assert.Equal("J-STO-ABCD1234", code);
    }
}
