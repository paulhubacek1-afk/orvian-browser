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
    private readonly List<string> _genericRules = [];
    private readonly object _sync = new();
    private bool _refreshStarted;

    public Blocker()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser");
        lock (_sync)
        {
            foreach (var host in BootstrapHosts) _blockedHosts.Add(host);
            _genericRules.AddRange(BuiltInGenericRules);
        }
        LoadCachedFilters();
    }

    public int RuleCount
    {
        get { lock (_sync) return _blockedHosts.Count + _genericRules.Count; }
    }

    public async Task RefreshFiltersAsync(CancellationToken cancellationToken = default)
    {
        if (_refreshStarted) return;
        _refreshStarted = true;
        var directory = GetFilterDirectory();
        Directory.CreateDirectory(directory);

        foreach (var url in FilterUrls)
        {
            try
            {
                var fileName = Path.Combine(directory, MakeSafeFileName(url));
                var shouldDownload = !File.Exists(fileName) || File.GetLastWriteTimeUtc(fileName) < DateTime.UtcNow.AddHours(-24);
                if (!shouldDownload) continue;

                using var response = await _http.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (content.Length > 25_000_000) continue;
                await File.WriteAllTextAsync(fileName, content, new UTF8Encoding(false), cancellationToken);
                ParseFilterText(content);
            }
            catch { }
        }
    }

    public bool ShouldBlock(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || string.IsNullOrWhiteSpace(parsed.Host)) return false;
        var host = NormalizeHost(parsed.Host);
        if (IsLocalOrPrivateHost(host) || IsAllowed(host)) return false;

        lock (_sync)
        {
            foreach (var rule in _blockedHosts)
                if (HostMatches(host, rule)) return true;

            foreach (var rule in _genericRules)
                if (GenericMatches(uri, rule)) return true;
        }
        return false;
    }

    public bool IsBlockedHost(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || string.IsNullOrWhiteSpace(parsed.Host)) return false;
        var host = NormalizeHost(parsed.Host);
        if (IsLocalOrPrivateHost(host) || IsAllowed(host)) return false;
        lock (_sync)
        {
            foreach (var rule in _blockedHosts)
                if (HostMatches(host, rule)) return true;
        }
        return false;
    }

    public void AllowSite(string host)
    {
        var normalized = NormalizeHost(host);
        if (!string.IsNullOrWhiteSpace(normalized)) _allowedHosts.Add(normalized);
    }

    public void RemoveSite(string host) => _allowedHosts.Remove(NormalizeHost(host));

    public bool IsAllowed(string host)
    {
        var normalized = NormalizeHost(host);
        lock (_sync)
        {
            foreach (var rule in _allowedHosts)
                if (normalized == rule || normalized.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase)) return true;
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

            if (!exception && IsUsefulGenericRule(line) && localGeneric.Count < 5000)
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

    private static bool HostMatches(string host, string rule) => host == rule || host.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase);

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
