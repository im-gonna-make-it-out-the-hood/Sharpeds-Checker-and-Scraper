using System.Security.Authentication;
using System.Text.Json.Serialization;
using ProxyScraperAndChecker.Scrapers;
using RobloxAssetFinder.Scrapers;

namespace ProxyScraperAndChecker; 

public static partial class Shared {
    [JsonSerializable(typeof(NucleousVPNProxies))]
    [JsonSerializable(typeof(ResidentialProxy_ProxySpider))]
    [JsonSerializable(typeof(string[][]))]
    public partial class Serializers : JsonSerializerContext { }
    public static HttpClient HttpClient =
        new HttpClient(new HttpClientHandler { SslProtocols = SslProtocols.Tls12, UseProxy = false, UseCookies = false, },
            true);
}