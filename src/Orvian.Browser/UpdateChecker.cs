using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Orvian.Browser;

public sealed record UpdateInfo(Version Version, string InstallerUrl);

public sealed class UpdateChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string ReleasesApi = "https://api.github.com/repos/paulhubacek1-afk/orvian-browser/releases?per_page=20";

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } version
            ? new Version(version.Major, version.Minor, Math.Max(0, version.Build))
            : new Version(0, 2, 2);

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                ReleasesApi + "&cache=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            request.Headers.UserAgent.ParseAdd("Orvian-Browser/0.2");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Array) return null;

            UpdateInfo? best = null;
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                if (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) continue;

                var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString()?.TrimStart('v', 'V') : null;
                if (!Version.TryParse(tag, out var version)) continue;

                string? installerUrl = null;
                if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) != true) continue;
                        if (asset.TryGetProperty("browser_download_url", out var urlElement))
                        {
                            installerUrl = urlElement.GetString();
                            if (!string.IsNullOrWhiteSpace(installerUrl)) break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(installerUrl)) continue;
                if (best is null || version > best.Version) best = new UpdateInfo(version, installerUrl);
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> DownloadInstallerAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"Orvian-Browser-Setup-{update.Version}.exe");
        try
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            using var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(tempPath);
            await input.CopyToAsync(output, cancellationToken);
            return File.Exists(tempPath) ? tempPath : null;
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return null;
        }
    }
}
