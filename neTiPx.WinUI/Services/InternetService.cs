using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace neTiPx.WinUI.Services
{
    public sealed class InternetService
    {
        private readonly string[] _ipServices =
        {
            "https://api.ipify.org",
            "http://api.ipify.org",
            "https://ifconfig.me/ip"
        };

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
                }
            }

            return "Fehler: keine Verbindung";
        }
    }
}
