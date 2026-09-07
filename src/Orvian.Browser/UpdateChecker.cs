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
            : new Version(0, 3, 0);

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        Version? publishedVersion = null;
        string? installerUrl = null;

        // Read VERSION.txt with a cache-buster first. This detects a newer stable build
        // even during the small window before GitHub has refreshed the latest release.
        try
        {
            var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{VersionUrl}?v={cacheBust}");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                if (Version.TryParse(text, out var remoteVersion))
                    publishedVersion = remoteVersion;
            }
        }
        catch { }

        // Fetch the latest release independently so an installer can be used as soon as
        // the CI release exists. Use the newer of VERSION.txt and the published release.
        try
        {
            var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ReleasesApi}?v={cacheBust}");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = json.RootElement;
                var tag = root.TryGetProperty("tag_name", out var tagElement)
                    ? tagElement.GetString()?.TrimStart('v', 'V')
                    : null;

                if (Version.TryParse(tag, out var releaseVersion))
                {
                    if (publishedVersion == null || releaseVersion > publishedVersion)
                        publishedVersion = releaseVersion;

                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                            if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) != true) continue;
                            if (asset.TryGetProperty("browser_download_url", out var urlElement))
                            {
                                var url = urlElement.GetString();
                                if (!string.IsNullOrWhiteSpace(url) && releaseVersion == publishedVersion)
                                {
                                    installerUrl = url;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        if (publishedVersion == null || publishedVersion <= CurrentVersion)
            return null;

        return new UpdateInfo(publishedVersion, installerUrl);
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser/0.3");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
