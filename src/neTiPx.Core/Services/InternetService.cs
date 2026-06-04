using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace neTiPx.Core.Services;

/// <summary>
/// Service to retrieve external/public IP address information.
/// </summary>
public sealed class InternetService
{
    private readonly string[] _ipServices =
    {
        "https://api.ipify.org",
        "http://api.ipify.org",
        "https://ifconfig.me/ip"
    };

    /// <summary>
    /// Loads the external IP address by querying multiple services.
    /// </summary>
    /// <returns>The external IP address or an error message.</returns>
    public async Task<string> LoadExternalIpAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        foreach (var url in _ipServices)
        {
            try
            {
                var response = await client.GetStringAsync(url);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    return response.Trim();
                }
            }
            catch
            {
                // Try next service
            }
        }

        return "Fehler: keine Verbindung";
    }
}
