using Microsoft.AspNetCore.Http;

namespace ElectronicLabNotebook.Services;

public static class ClientDeviceDetector
{
    public static bool IsIosClient(HttpRequest request)
    {
        var userAgent = request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        return userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase)
            || (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)
                && userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase));
    }
}
