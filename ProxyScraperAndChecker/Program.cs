// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using ProxyScraperAndChecker;
using ProxyScraperAndChecker.Scrapers;
using RobloxAssetFinder.Scrapers;

Console.WriteLine("Initializing...");
if (!ThreadPool.SetMinThreads(Environment.ProcessorCount * 12, Environment.ProcessorCount))
    throw new ExternalException("Environment disallows threadpool changes? wtf");

var ProxyUrlTargets = new string[] {
    "https://raw.githubusercontent.com/ALIILAPRO/Proxy/main/http.txt",
    "https://raw.githubusercontent.com/ShiftyTR/Proxy-List/master/http.txt",
    "https://raw.githubusercontent.com/prxchk/proxy-list/main/http.txt",
    "https://raw.githubusercontent.com/officialputuid/KangProxy/KangProxy/http/http.txt", //! MAY CONTAIN MALFORMED PROXIES!
    "https://raw.githubusercontent.com/UptimerBot/proxy-list/master/proxies/http.txt",
    "https://raw.githubusercontent.com/Bardiafa/Proxy-Leecher/main/proxies.txt", //! MAY CONTAIN MALFORMED PROXIES!
    "https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/http.txt",
};
var stopWatch = Stopwatch.StartNew();
var proxyScraper = await ScrapeProxies(ProxyUrlTargets, Shared.HttpClient);
stopWatch.Stop();
Console.WriteLine($"Scraping finalized in {stopWatch.ElapsedMilliseconds}ms");
Console.WriteLine(
    "Starting proxy checking. We will attempt to establish a TCP connection with all proxies, with a timeout of 5 seconds!");

Console.WriteLine("Press any key to continue with the checking, else, press CTRL+C to exit.");

Console.ReadKey();

Console.WriteLine("Proceeding in 5 seconds...");

await Task.Delay(5000);

stopWatch.Restart();
var proxies = await CheckProxyAsync(proxyScraper);
stopWatch.Stop();
Console.WriteLine($"Checking finalized in {stopWatch.ElapsedMilliseconds}ms");

File.Delete("proxies_unchecked.txt");
File.Delete("proxies_checked.txt");

Console.WriteLine("Writing ALL proxies to proxies_unchecked.txt");
await File.WriteAllLinesAsync("proxies_unchecked.txt", proxyScraper, Encoding.UTF8);
Console.WriteLine("Writing VALID proxies to proxies_checked.txt");
await File.WriteAllLinesAsync("proxies_checked.txt", proxies, Encoding.UTF8);

foreach (var prox in proxies) { }

Shared.HttpClient.Dispose();

static async Task<string[]> CheckProxyAsync(string[] proxies) {
    Console.WriteLine("Checker Initializing!");
    var totalProxies = proxies.Length;

    var semicolonCount = proxies.Count(x => x.Contains(':'));

    if (semicolonCount != totalProxies) { // Proxies are malformed.
        Console.WriteLine(
            $"[Proxy Checker] {semicolonCount - totalProxies}/{totalProxies} proxies are malformed! Checking operation can not continue!");
        return Array.Empty<string>();
    }

    static async Task<ProxyResult> CheckProxyAsync(string host, ushort port) {
        CancellationTokenSource cts = new(5000);
        Socket sock = new(SocketType.Stream, ProtocolType.Tcp);
        try {
            await sock.ConnectAsync(new IPEndPoint(IPAddress.Parse(host), port), cts.Token);
            Console.WriteLine($"[Proxy Checker] Send CONNECT request to {host}:{port}");
            await sock.SendAsync("CONNECT http://google.com/ HTTP/1.1\n"u8.ToArray());

            //Console.WriteLine("[Proxy Checker] Waiting for a response from the proxy... (3 seconds)");

            var buf = new byte[1024];
            sock.ReceiveTimeout = 5000; // 5 Seconds.
            var read = sock.Receive(buf, 0, buf.Length, SocketFlags.None, out var error);

            if (read == 0 && error == SocketError.TimedOut) {
                Console.WriteLine("[Proxy Checker] Proxy didn't respond! Cleaning up...");
                await sock.DisconnectAsync(false);
                return new(host, port, false);
            }

            Console.WriteLine("[Proxy Checker] Check passed! Cleaning up...");
            await sock.DisconnectAsync(false);
            return new(host, port, true);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) {
            //Console.WriteLine($"[Proxy Checker] Failed to check proxy! {host}:{port}");
        }
        catch (FormatException) {
            Console.WriteLine($"[Proxy Checker] The host was malformed! {host}:{port}");
        }
        catch (SocketException ex) {
            Console.WriteLine($"[Proxy Checker] The proxy likely refused the connection! {host}:{port}");
        }
        finally {
            sock.Dispose();
        }

        return new(host, port, false);
    }

    List<Task<ProxyResult>> tasks = new(totalProxies);
    for (var i = 0; i < totalProxies; i++) {
        if (proxies[i] == "") continue; // Skip empty char.

        var proxy = proxies[i].Split(':');

        if (ushort.TryParse(proxy[1], out var port)) {
            tasks.Add(CheckProxyAsync(proxy[0], port));
        }
        else {
            Console.WriteLine("[Proxy Checker] Failure with proxy " + proxies[i]);
        }
    }

    await Task.WhenAll(tasks);
    Console.WriteLine("[Proxy Checker] Checker Finished! Serializing results...");

    List<string> results = new(totalProxies);
    var successInt = 0;
    StringBuilder sb = new(64);
    foreach (var task in tasks) {
        if (!task.Result.isSuccess) continue;

        successInt++;
        sb.Clear();
        results.Add(sb.Append(task.Result.host).Append(':').Append(task.Result.port).ToString());
    }

    Console.WriteLine($"[Proxy Checker] {successInt}/{totalProxies} passed the check.");

    return results.ToArray();
    
}

static async Task<string[]> ScrapeProxies(string[] targets, HttpClient client) {
    List<string> proxyList = new List<string>(65000); // We scrape normally like 60K proxies or less.

    List<Task<HttpResponseMessage>> tasks = new(targets.Length);

    for (var i = 0; i < targets.Length; i++) {
        var target = targets[i];
        tasks.Add(Task.Run(async () => {
            try {
                return await client.GetAsync(target);
            }
            catch (Exception ex) {
                throw new Exception($"[Proxy Scraper] Failed to connect to {target}", ex);
            }
        }));
    }

    Console.WriteLine("[Proxy Scraper] Downloading all proxy lists...");
    await Task.WhenAll(tasks);

    foreach (var task in tasks) {
        var response = await task;
        if (response.IsSuccessStatusCode) {
            var proxies = await response.Content.ReadAsStringAsync();
            var strArr = Array.Empty<string>();
            if (proxies.Contains("\r\n")) {
                strArr = proxies.Split("\r\n");
            }
            else if (proxies.Contains('\n')) {
                strArr = proxies.Split('\n');
            }

            foreach (var str in strArr) {
                if (!string.IsNullOrEmpty(str) && str.Length <= 21) {
                    // Avoid "" characters. && Size 21 due to 255.255.255.255:65535's length, it shouldn't be larger than that, else we KNOW its a malformed proxy, and we should ignore it!
                    proxyList.Add(str);
                }
            }

            Console.WriteLine(
                $"[Proxy Scraper] Received {response.StatusCode} from server for {response.RequestMessage.RequestUri.OriginalString}. Obtained {strArr.Length} proxies");
        }
        else {
            Console.WriteLine(
                $"[Proxy Scraper] Request failure! We have failed to establish a connection to {response.RequestMessage.RequestUri.OriginalString}! Server reported the following status code: {response.StatusCode}");
        }
    }

    Console.WriteLine("[Proxy Scraper] Scraping FreeProxyLists...");
    var proxies_freeproxylists = await FreeProxyListsScraper.GetProxiesAsync();
    Console.WriteLine($"[Proxy Scraper] Obtained {proxies_freeproxylists.Length} proxies from scraping...");
    proxyList.AddRange(proxies_freeproxylists);

    Console.WriteLine("[Proxy Scraper] Scraping NucleusVPN Proxies...");
    var proxies_nucleousvpn = await NucleousVPNProxies.GetProxies();
    List<string> proxiesFoundInNucleusQuery = new(proxies_nucleousvpn.proxy_list.Length);
    
    foreach (var proxy in proxies_nucleousvpn.proxy_list)
        proxiesFoundInNucleusQuery.Add(proxy.host);
    
    Console.WriteLine($"[Proxy Scraper] Obtained {proxies_nucleousvpn.proxy_list.Length} proxies from scraping...");
    proxyList.AddRange(proxiesFoundInNucleusQuery);

    Console.WriteLine($"[Proxy Scraper] Total proxies downloaded: {proxyList.Count}.");

    Console.WriteLine("[Proxy Scraper] Removing possible duplicates...");


    HashSet<string> hashSet = new(proxyList);

    var foundDupes = proxyList.Count - hashSet.Count;
    var processedProxies = proxyList.Count;

    Console.WriteLine(
        $"[Proxy Scraper] Processed proxies! Removed {foundDupes} duplicates out of {processedProxies} proxies.");
    return hashSet.ToArray();
}