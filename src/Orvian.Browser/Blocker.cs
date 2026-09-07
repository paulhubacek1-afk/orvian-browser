using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Orvian.Browser;

public sealed class Blocker
{
    private static readonly string[] FilterUrls =
    [
        "https://ublockorigin.github.io/uAssets/thirdparties/easylist.txt",
        "https://ublockorigin.github.io/uAssets/thirdparties/easyprivacy.txt"
    ];

    private static readonly string[] BootstrapHosts =
    [
        "doubleclick.net", "googlesyndication.com", "googleadservices.com", "googletagmanager.com",
        "adnxs.com", "scorecardresearch.com", "outbrain.com", "taboola.com", "zedo.com",
        "adsafeprotected.com", "advertising.com", "adform.net", "adsrvr.org", "amazon-adsystem.com",
        "criteo.com", "criteo.net", "demdex.net", "mathtag.com", "rubiconproject.com",
        "quantserve.com", "hotjar.com", "mixpanel.com", "segment.io", "segment.com",
        "analytics.google.com", "connect.facebook.net", "facebook.net", "clarity.ms"
    ];

    private static readonly string[] BuiltInGenericRules =
    [
        "/ads.js", "/adservice", "/pagead/", "/advertising/", "/doubleclick/", "/analytics.js",
        "googletagmanager.com/gtm.js", "google-analytics.com/analytics.js"
    ];

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly HashSet<string> _blockedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedSuffixes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _genericRules = [];
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<string, bool> _decisionCache = new(StringComparer.OrdinalIgnoreCase);
    private int _refreshStarted;
    private int _filtersLoaded;

    public Blocker()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser");
        foreach (var host in BootstrapHosts) _blockedHosts.Add(host);
        _genericRules.AddRange(BuiltInGenericRules);
        // Do not synchronously parse 10–25 MB filter lists during window construction.
        // The warm-up task loads them off the UI path instead.
    }

    public int RuleCount
    {
        get { lock (_sync) return _blockedHosts.Count + _genericRules.Count; }
    }

    public async Task RefreshFiltersAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _refreshStarted, 1) != 0) return;
        var directory = GetFilterDirectory();
        Directory.CreateDirectory(directory);

        foreach (var url in FilterUrls)
        {
            try
            {
                var fileName = Path.Combine(directory, MakeSafeFileName(url));
                var shouldDownload = !File.Exists(fileName) || File.GetLastWriteTimeUtc(fileName) < DateTime.UtcNow.AddHours(-24);
                if (shouldDownload)
                {
                    using var response = await _http.GetAsync(url, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        if (content.Length <= 25_000_000)
                            await File.WriteAllTextAsync(fileName, content, new UTF8Encoding(false), cancellationToken);
                    }
                }

                if (File.Exists(fileName))
                {
                    var content = await File.ReadAllTextAsync(fileName, cancellationToken);
                    if (content.Length <= 25_000_000)
                        await Task.Run(() => ParseFilterText(content), cancellationToken);
                }
            }
            catch { }
        }

        Volatile.Write(ref _filtersLoaded, 1);
        _decisionCache.Clear();
    }

    public bool ShouldBlock(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || string.IsNullOrWhiteSpace(parsed.Host)) return false;
        var host = NormalizeHost(parsed.Host);
        if (IsLocalOrPrivateHost(host) || IsAllowed(host)) return false;

        if (_decisionCache.TryGetValue(uri, out var cached)) return cached;

        var blocked = false;
        lock (_sync)
        {
            // O(number-of-labels) domain lookup instead of scanning every filter entry.
            blocked = MatchesBlockedHost(host);
            if (!blocked && ShouldRunGenericScan(uri))
                foreach (var rule in _genericRules)
                    if (GenericMatches(uri, rule)) { blocked = true; break; }
        }

        // Keep the cache bounded so resource-heavy pages cannot grow it forever.
        if (_decisionCache.Count < 8192) _decisionCache.TryAdd(uri, blocked);
        return blocked;
    }

    public bool IsBlockedHost(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || string.IsNullOrWhiteSpace(parsed.Host)) return false;
        var host = NormalizeHost(parsed.Host);
        if (IsLocalOrPrivateHost(host) || IsAllowed(host)) return false;
        lock (_sync) return MatchesBlockedHost(host);
    }

    public void AllowSite(string host)
    {
        var normalized = NormalizeHost(host);
        if (string.IsNullOrWhiteSpace(normalized)) return;
        lock (_sync)
        {
            _allowedHosts.Add(normalized);
            _allowedSuffixes.Add(normalized);
        }
        _decisionCache.Clear();
    }

    public void RemoveSite(string host)
    {
        var normalized = NormalizeHost(host);
        lock (_sync)
        {
            _allowedHosts.Remove(normalized);
            _allowedSuffixes.Remove(normalized);
        }
        _decisionCache.Clear();
    }

    public bool IsAllowed(string host)
    {
        var normalized = NormalizeHost(host);
        lock (_sync)
        {
            foreach (var rule in _allowedSuffixes)
                if (normalized == rule || normalized.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private bool MatchesBlockedHost(string host)
    {
        if (_blockedHosts.Contains(host)) return true;
        var dot = host.IndexOf('.');
        while (dot >= 0 && dot < host.Length - 1)
        {
            var parent = host[(dot + 1)..];
            if (_blockedHosts.Contains(parent)) return true;
            dot = host.IndexOf('.', dot + 1);
        }
        return false;
    }

    private void LoadCachedFilters()
    {
        try
        {
            var directory = GetFilterDirectory();
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory, "*.txt"))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length <= 25_000_000) ParseFilterText(File.ReadAllText(file));
                }
                catch { }
            }
        }
        catch { }
    }

    private void ParseFilterText(string text)
    {
        var localBlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localGeneric = new List<string>();

        using var reader = new StringReader(text);
        string? raw;
        while ((raw = reader.ReadLine()) is not null)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('!') || line.StartsWith('[')) continue;
            if (line.Contains("##", StringComparison.Ordinal) || line.Contains("#@#", StringComparison.Ordinal)) continue;

            var exception = line.StartsWith("@@", StringComparison.Ordinal);
            if (exception) line = line[2..];

            if (line.StartsWith("||", StringComparison.Ordinal))
            {
                var host = ExtractHost(line[2..]);
                if (!string.IsNullOrWhiteSpace(host))
                {
                    if (exception) localAllowed.Add(host);
                    else localBlocked.Add(host);
                }
                continue;
            }

            if (TryParseHostsLine(line, out var hostsEntry))
            {
                if (exception) localAllowed.Add(hostsEntry);
                else localBlocked.Add(hostsEntry);
                continue;
            }

            if (!exception && IsUsefulGenericRule(line) && localGeneric.Count < 1500)
                localGeneric.Add(line);
        }

        lock (_sync)
        {
            foreach (var value in localBlocked) _blockedHosts.Add(value);
            foreach (var value in localAllowed) _allowedHosts.Add(value);
            foreach (var value in localGeneric)
                if (!_genericRules.Contains(value, StringComparer.OrdinalIgnoreCase)) _genericRules.Add(value);
        }
    }

    private static bool ShouldRunGenericScan(string uri)
    {
        return uri.Contains("/ad", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("ads", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("advert", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("analytics", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("tracking", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("doubleclick", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("pixel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseHostsLine(string line, out string host)
    {
        host = string.Empty;
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        if (parts[0] != "0.0.0.0" && parts[0] != "127.0.0.1" && parts[0] != "::1") return false;
        host = NormalizeHost(parts[1]);
        return host.Length > 0 && !IPAddress.TryParse(host, out _);
    }

    private static string ExtractHost(string pattern)
    {
        var stop = pattern.IndexOfAny(new[] { '^', '/', '*', '$', '?', '|' });
        if (stop >= 0) pattern = pattern[..stop];
        return NormalizeHost(pattern);
    }

    private static bool IsUsefulGenericRule(string line)
    {
        if (line.StartsWith("/", StringComparison.Ordinal) && line.EndsWith("/", StringComparison.Ordinal)) return false;
        if (line.Length < 5 || line.Length > 180) return false;
        if (line.StartsWith("#", StringComparison.Ordinal)) return false;
        return line.Contains("ad", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("analytics", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("doubleclick", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("tracking", StringComparison.OrdinalIgnoreCase);
    }

    private static bool GenericMatches(string uri, string rule)
    {
        var pattern = rule.Replace("*", string.Empty, StringComparison.Ordinal);
        if (pattern.StartsWith("/", StringComparison.Ordinal)) pattern = pattern[1..];
        return pattern.Length >= 4 && uri.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalOrPrivateHost(string host)
    {
        if (host == "localhost" || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;
        if (IPAddress.TryParse(host, out var ip)) return IPAddress.IsLoopback(ip) || IsPrivateIp(ip);
        return false;
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
            return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254);
        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
    }

    private static string NormalizeHost(string host)
    {
        var value = host.Trim().Trim('.').ToLowerInvariant();
        return value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
    }

    private static string GetFilterDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "Filters");

    private static string MakeSafeFileName(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".txt";
}
