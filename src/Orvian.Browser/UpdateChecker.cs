using System.Net.Http;
using System.Text.Json;

namespace Orvian.Browser;

public sealed class UpdateChecker
{
    private static readonly HttpClient Http = new();
    private const string ReleasesApi = "https://api.github.com/repos/paulhubacek1-afk/orvian-browser/releases/latest";
    public Version CurrentVersion { get; } = new(0, 1, 0);

    public async Task<Version?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
        request.Headers.UserAgent.ParseAdd("Orvian-Browser");
        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        return Version.TryParse(tag, out var version) ? version : null;
    }
}