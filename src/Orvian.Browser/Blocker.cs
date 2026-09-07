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

    private static readonly string[] BuiltInPathFragments =
    [
        "/ads.js", "/adservice", "/pagead/", "/advertising/", "/doubleclick/",
        "/analytics.js", "/gtm.js", "/collect?", "/tracking.js", "/pixel"
    ];

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly HashSet<string> _blockedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pathFragments = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private int _refreshStarted;

    public Blocker()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser/1.0");
        lock (_sync)
        {
            foreach (var host in BootstrapHosts)
                _blockedHosts.Add(host);
            foreach (var fragment in BuiltInPathFragments)
                _pathFragments.Add(fragment);
        }
    }

    public int RuleCount
    {
        get
        {
            lock (_sync)
                return _blockedHosts.Count + _pathFragments.Count;
        }
    }

    public async Task RefreshFiltersAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _refreshStarted, 1) != 0)
            return;

        var directory = GetFilterDirectory();
        Directory.CreateDirectory(directory);

        // Loading the local cache asynchronously keeps browser startup responsive.
        try
        {
            await Task.Run(LoadCachedFilters, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch { }

        foreach (var url in FilterUrls)
        {
            try
            {
                var fileName = Path.Combine(directory, MakeSafeFileName(url));
                var shouldDownload = !File.Exists(fileName) ||
                                     File.GetLastWriteTimeUtc(fileName) < DateTime.UtcNow.AddHours(-24);

                if (!shouldDownload)
                    continue;

                using var response = await _http.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    continue;

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (content.Length is <= 0 or > 25_000_000)
                    continue;

                await File.WriteAllTextAsync(
                    fileName,
                    content,
                    new UTF8Encoding(false),
                    cancellationToken);

                ParseFilterText(content);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // One upstream list failing must not disable the other list or browsing.
            }
        }
    }

    public bool ShouldBlock(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.Host))
            return false;

        // Only network resources are filter candidates. Documents, file://, data:// and
        // internal browser pages are deliberately left alone by the caller and this method.
        if (!parsed.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !parsed.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return false;

        var host = NormalizeHost(parsed.Host);
        if (IsLocalOrPrivateHost(host) || IsAllowed(host))
            return false;

        lock (_sync)
        {
            if (MatchesHostFast(host))
                return true;

            var path = parsed.PathAndQuery;
            foreach (var fragment in _pathFragments)
            {
                if (path.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public void AllowSite(string host)
    {
        var normalized = NormalizeHost(host);
        if (normalized.Length == 0) return;

        lock (_sync)
            _allowedHosts.Add(normalized);
    }

    public void RemoveSite(string host)
    {
        var normalized = NormalizeHost(host);
        lock (_sync)
            _allowedHosts.Remove(normalized);
    }

    public bool IsAllowed(string host)
    {
        var normalized = NormalizeHost(host);

        lock (_sync)
        {
            if (_allowedHosts.Contains(normalized))
                return true;

            var labels = normalized.Split('.');
            for (var i = 1; i < labels.Length - 1; i++)
            {
                var suffix = string.Join('.', labels[i..]);
                if (_allowedHosts.Contains(suffix))
                    return true;
            }

            return false;
        }
    }

    private bool MatchesHostFast(string host)
    {
        if (_blockedHosts.Contains(host))
            return true;

        var start = host.IndexOf('.');
        while (start > 0 && start < host.Length - 1)
        {
            var suffix = host[(start + 1)..];
            if (_blockedHosts.Contains(suffix))
                return true;

            start = host.IndexOf('.', start + 1);
        }

        return false;
    }

    private void LoadCachedFilters()
    {
        try
        {
            var directory = GetFilterDirectory();
            if (!Directory.Exists(directory))
                return;

            foreach (var file in Directory.EnumerateFiles(directory, "*.txt"))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length <= 25_000_000)
                        ParseFilterText(File.ReadAllText(file));
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

        using var reader = new StringReader(text);
        string? raw;

        while ((raw = reader.ReadLine()) is not null)
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('!') || line.StartsWith('['))
                continue;

            // Cosmetic filters need a DOM engine and are intentionally ignored here.
            if (line.Contains("##", StringComparison.Ordinal) ||
                line.Contains("#@#", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("@@||", StringComparison.Ordinal))
            {
                var host = ExtractHost(line[4..]);
                if (!string.IsNullOrWhiteSpace(host))
                    localAllowed.Add(host);
                continue;
            }

            if (line.StartsWith("||", StringComparison.Ordinal))
            {
                var host = ExtractHost(line[2..]);
                if (!string.IsNullOrWhiteSpace(host) && host.IndexOf('.') > 0)
                    localBlocked.Add(host);
                continue;
            }

            if (TryParseHostsLine(line, out var hostsEntry))
                localBlocked.Add(hostsEntry);
        }

        lock (_sync)
        {
            foreach (var host in localBlocked)
                _blockedHosts.Add(host);

            foreach (var host in localAllowed)
                _allowedHosts.Add(host);
        }
    }

    private static bool TryParseHostsLine(string line, out string host)
    {
        host = string.Empty;

        var parts = line.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return false;

        if (parts[0] is not ("0.0.0.0" or "127.0.0.1" or "::1"))
            return false;

        host = NormalizeHost(parts[1]);
        return host.Length > 0 && !IPAddress.TryParse(host, out _);
    }

    private static string ExtractHost(string pattern)
    {
        var stop = pattern.IndexOfAny(new[] { '^', '/', '*', '$', '?', '|' });
        if (stop >= 0)
            pattern = pattern[..stop];

        return NormalizeHost(pattern);
    }

    private static bool IsLocalOrPrivateHost(string host)
    {
        if (host == "localhost" ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(host, out var ip))
            return IPAddress.IsLoopback(ip) || IsPrivateIp(ip);

        return false;
    }

    private static bool IsPrivateIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }

        return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
    }

    private static string NormalizeHost(string host)
    {
        var value = host.Trim().Trim('.').ToLowerInvariant();
        return value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? value[4..]
            : value;
    }

    private static string GetFilterDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Orvian",
            "Filters");

    private static string MakeSafeFileName(string url)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".txt";
}
