using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Orvian.Browser;

public sealed record UpdateInfo(Version Version, string? InstallerUrl)
{
    public bool HasInstaller => !string.IsNullOrWhiteSpace(InstallerUrl);
}

public sealed class UpdateChecker
{
    private static readonly HttpClient Http = CreateHttpClient();
    private const string ReleasesApi = "https://api.github.com/repos/paulhubacek1-afk/orvian-browser/releases/latest";
    private const string VersionUrl = "https://raw.githubusercontent.com/paulhubacek1-afk/orvian-browser/main/VERSION.txt";
    public const string ReleasesPage = "https://github.com/paulhubacek1-afk/orvian-browser/releases/latest";

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? new Version(version.Major, version.Minor, Math.Max(0, version.Build))
            : new Version(0, 3, 1);

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        // First ask the raw VERSION.txt for the authoritative remote version. A timestamp
        // prevents stale proxy/CDN responses from making the browser appear up to date.
        var remoteVersion = await GetRemoteVersionAsync(cancellationToken);
        if (remoteVersion == null || remoteVersion <= CurrentVersion) return null;

        // Only resolve the release asset after a newer version was confirmed. This keeps
        // update checks tiny and still works when the release is created a little later.
        try
        {
            var releaseUrl = ReleasesApi + "?orvian=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = json.RootElement;
                var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString()?.TrimStart('v', 'V') : null;
                if (Version.TryParse(tag, out var releaseVersion) && releaseVersion >= remoteVersion)
                {
                    string? installerUrl = null;
                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                            if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) != true) continue;
                            if (asset.TryGetProperty("browser_download_url", out var urlElement))
                            {
                                installerUrl = urlElement.GetString();
                                break;
                            }
                        }
                    }
                    return new UpdateInfo(releaseVersion, installerUrl);
                }
            }
        }
        catch { }

        return new UpdateInfo(remoteVersion, null);
    }

    private static async Task<Version?> GetRemoteVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url = VersionUrl + "?orvian=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            return Version.TryParse(text, out var version) ? version : null;
        }
        catch { return null; }
    }

    public async Task<string?> DownloadInstallerAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (!update.HasInstaller || update.InstallerUrl == null) return null;
        var tempPath = Path.Combine(Path.GetTempPath(), $"Orvian-Browser-Setup-{update.Version}.exe");
        using var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(tempPath);
        await input.CopyToAsync(output, cancellationToken);
        return File.Exists(tempPath) ? tempPath : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser/0.3.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
