using System.Net.Http;
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

    private const string ReleasesApi = "https://api.github.com/repos/paulhubacek1-afk/orvian-browser/releases?per_page=30";
    private const string VersionUrl = "https://raw.githubusercontent.com/paulhubacek1-afk/orvian-browser/main/VERSION.txt";
    public const string ReleasesPage = "https://github.com/paulhubacek1-afk/orvian-browser/releases/latest";

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? new Version(Math.Max(0, version.Major), Math.Max(0, version.Minor), Math.Max(0, version.Build))
            : new Version(1, 0, 0);

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        // Do not trust GitHub's single "latest" pointer. Scan stable releases and
        // VERSION.txt, then use the highest semantic version we can find.
        var remoteTask = GetRemoteVersionAsync(cancellationToken);
        var releasesTask = GetStableReleasesAsync(cancellationToken);

        try
        {
            await Task.WhenAll(remoteTask, releasesTask);

            var remoteVersion = await remoteTask;
            var releases = await releasesTask;

            Version? latest = remoteVersion;
            string? installerUrl = null;

            foreach (var release in releases)
            {
                if (latest is null || release.Version > latest)
                {
                    latest = release.Version;
                    installerUrl = release.InstallerUrl;
                }
                else if (release.Version == latest && installerUrl is null)
                {
                    installerUrl = release.InstallerUrl;
                }
            }

            return latest is null ? null : new UpdateInfo(latest, installerUrl);
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
        var cacheBust = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{VersionUrl}?orvian_check={cacheBust}");

        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        request.Headers.Pragma.ParseAdd("no-cache");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        return TryParseVersion(text, out var version) ? version : null;
    }

    private static async Task<List<(Version Version, string? InstallerUrl)>> GetStableReleasesAsync(CancellationToken cancellationToken)
    {
        var cacheBust = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ReleasesApi}&orvian_check={cacheBust}");

        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        request.Headers.Pragma.ParseAdd("no-cache");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new List<(Version, string?)>();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var result = new List<(Version, string?)>();
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var root in json.RootElement.EnumerateArray())
        {
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean())
                continue;
            if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean())
                continue;

            var tag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;

            if (!TryParseVersion(tag, out var version))
                continue;

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

            result.Add((version, installerUrl));
        }

        return result;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim().TrimStart('v', 'V');
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1 || parts.Length > 4)
            return false;

        if (!parts.All(static part => int.TryParse(part, out _)))
            return false;

        return Version.TryParse(value, out version!);
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
            Timeout = TimeSpan.FromSeconds(12)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Orvian-Browser/1.0 (+https://github.com/paulhubacek1-afk/orvian-browser)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }
}
