using System.Net.Http.Headers;
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
            : new Version(1, 0, 0);

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // The tiny version file detects a new release even before the GitHub Release has
            // finished publishing its installer. The release endpoint supplies the installer.
            var remoteTask = GetRemoteVersionAsync(cancellationToken);
            var releaseTask = GetLatestReleaseAsync(cancellationToken);

            await Task.WhenAll(remoteTask, releaseTask);

            var remoteVersion = await remoteTask;
            var release = await releaseTask;

            Version? latest = remoteVersion;
            string? installerUrl = null;

            if (release is not null && (latest is null || release.Value.Version > latest))
                latest = release.Value.Version;

            if (latest is null)
                return null;

            if (release is not null && release.Value.Version == latest)
                installerUrl = release.Value.InstallerUrl;

            return new UpdateInfo(latest, installerUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Version?> GetRemoteVersionAsync(CancellationToken cancellationToken)
    {
        var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{VersionUrl}?t={cacheBust}");

        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        request.Headers.Pragma.ParseAdd("no-cache");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        return Version.TryParse(text, out var version) ? version : null;
    }

    private static async Task<(Version Version, string? InstallerUrl)?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        var cacheBust = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ReleasesApi}?t={cacheBust}");

        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        request.Headers.Pragma.ParseAdd("no-cache");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var root = json.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement)
            ? tagElement.GetString()?.TrimStart('v', 'V')
            : null;

        if (!Version.TryParse(tag, out var version))
            return null;

        string? installerUrl = null;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null;

                if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) != true)
                    continue;

                if (asset.TryGetProperty("browser_download_url", out var urlElement))
                {
                    installerUrl = urlElement.GetString();
                    break;
                }
            }
        }

        return (version, installerUrl);
    }

    public async Task<string?> DownloadInstallerAsync(
        UpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        if (!update.HasInstaller || update.InstallerUrl == null)
            return null;

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"Orvian-Browser-Setup-{update.Version}.exe");

        using var response = await Http.GetAsync(
            update.InstallerUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(tempPath);
        await input.CopyToAsync(output, cancellationToken);

        return File.Exists(tempPath) ? tempPath : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
