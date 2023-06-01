using System.Text.Json;
using ProxyScraperAndChecker;

namespace RobloxAssetFinder.Scrapers;

public class ProxySpiderClient {
    public string proxyUsername;
    public string proxyPassword;
    public string apiKey;

    public ProxySpiderClient(string proxyUsername, string proxyPassword, string apiKey) {
        this.proxyUsername = proxyUsername;
        this.proxyPassword = proxyPassword;
        this.apiKey = apiKey;
    }

    public async Task<ResidentialProxy_ProxySpider> GetProxies() {
        var ApiUrl =
            $"https://proxy-spider.com/api/residential-proxies.json?api_key={apiKey}&action=get_proxies";
        return JsonSerializer.Deserialize<ResidentialProxy_ProxySpider>(await Shared.HttpClient.GetStringAsync(ApiUrl),
            Shared.Serializers.Default.ResidentialProxy_ProxySpider);
    }
}

public class ResidentialProxy_ProxySpider {
    public string status { get; set; }
    public Data data { get; set; }
    public string message { get; set; }
    public int request_id { get; set; }
}

public class Data {
    public string[] proxies { get; set; }
}