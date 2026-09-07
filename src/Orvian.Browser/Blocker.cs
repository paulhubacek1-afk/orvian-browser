using System.Text.RegularExpressions;

namespace Orvian.Browser;

public sealed class Blocker
{
    // Small bootstrap list. Production builds should ship periodically updated, license-compliant filter lists.
    private static readonly string[] Hosts =
    [
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "adnxs.com", "scorecardresearch.com", "facebook.net", "connect.facebook.net",
        "analytics.google.com", "googletagmanager.com"
    ];

    private readonly HashSet<string> _allowlistedHosts = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldBlock(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || string.IsNullOrEmpty(parsed.Host)) return false;
        if (IsAllowed(parsed.Host)) return false;
        var host = parsed.Host.TrimStart('.').ToLowerInvariant();
        return Hosts.Any(h => host == h || host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)) &&
               !Regex.IsMatch(uri, @"[?&](login|auth|oauth)=", RegexOptions.IgnoreCase);
    }

    public bool IsBlockedHost(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
        var host = parsed.Host.ToLowerInvariant();
        return Hosts.Any(h => host == h) || host.StartsWith("www.") && Hosts.Any(h => host[4..] == h);
    }

    public void AllowSite(string host) => _allowlistedHosts.Add(host);
    public void RemoveSite(string host) => _allowlistedHosts.Remove(host);
    public bool IsAllowed(string host) => _allowlistedHosts.Contains(host);
}