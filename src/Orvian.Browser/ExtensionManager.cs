using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace Orvian.Browser;

internal sealed class ExtensionManager
{
    internal const string SpotifyExtensionId = "spotify";
    private readonly string _extensionRoot = Path.Combine(AppContext.BaseDirectory, "extensions");

    internal sealed record ExtensionCatalogItem(
        string Id,
        string Name,
        string Version,
        string Description,
        string FolderName,
        string Category);

    internal static readonly IReadOnlyList<ExtensionCatalogItem> Catalog = new[]
    {
        new ExtensionCatalogItem(
            SpotifyExtensionId,
            "Spotify",
            "1.0.0",
            "Unoffizieller Orvian-Spotify-Controller mit schwebendem Mini-Player, Wiedergabesteuerung und eigener Queue.",
            "Spotify",
            "Musik")
    };

    internal string GetExtensionFolder(string folderName) => Path.Combine(_extensionRoot, folderName);

    internal async Task<IReadOnlyList<CoreWebView2BrowserExtension>> GetInstalledAsync(CoreWebView2Profile? profile)
    {
        if (profile == null) return Array.Empty<CoreWebView2BrowserExtension>();
        return (await profile.GetBrowserExtensionsAsync()).ToArray();
    }

    internal async Task<CoreWebView2BrowserExtension?> FindByNameAsync(CoreWebView2Profile? profile, string name)
    {
        if (profile == null) return null;
        var extensions = await profile.GetBrowserExtensionsAsync();
        return extensions.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    internal async Task<CoreWebView2BrowserExtension> InstallAsync(CoreWebView2Profile profile, ExtensionCatalogItem item)
    {
        var folder = GetExtensionFolder(item.FolderName);
        ValidateExtensionFolder(folder);

        var existing = await FindByNameAsync(profile, item.Name);
        if (existing != null)
        {
            if (!existing.IsEnabled) await existing.EnableAsync(true);
            return existing;
        }

        return await profile.AddBrowserExtensionAsync(folder);
    }

    internal Task RemoveAsync(CoreWebView2BrowserExtension extension) => extension.RemoveAsync();

    internal Task SetEnabledAsync(CoreWebView2BrowserExtension extension, bool enabled)
        => extension.EnableAsync(enabled);

    private static void ValidateExtensionFolder(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Die Extension wurde nicht gefunden: {folder}");

        var manifestPath = Path.Combine(folder, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("manifest.json fehlt in der Extension.", manifestPath);

        using var stream = File.OpenRead(manifestPath);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("manifest_version", out var version) || version.GetInt32() < 2)
            throw new InvalidDataException("Die Extension verwendet kein gültiges Chrome-Manifest.");
    }
}
