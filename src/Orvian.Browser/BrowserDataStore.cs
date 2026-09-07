using System.Text.Json;

namespace Orvian.Browser;

public sealed record HistoryEntry(string Url, string Title, DateTimeOffset VisitedAt);
public sealed record BookmarkEntry(string Url, string Title, DateTimeOffset AddedAt);
public sealed record PermissionEntry(string Origin, string Permission, string Decision, DateTimeOffset DecidedAt);

public sealed class BrowserDataStore
{
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "Data");
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public BrowserDataStore() => Directory.CreateDirectory(_root);
    private string PathFor(string name) => Path.Combine(_root, name);

    public async Task AddHistoryAsync(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        var items = await ReadAsync<HistoryEntry>("history.json");
        items.Insert(0, new HistoryEntry(url, string.IsNullOrWhiteSpace(title) ? url : title, DateTimeOffset.Now));
        var unique = items.GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(500).ToList();
        await WriteAsync("history.json", unique);
    }

    public Task<List<HistoryEntry>> GetHistoryAsync() => ReadAsync<HistoryEntry>("history.json");

    public async Task ToggleBookmarkAsync(string url, string title)
    {
        var items = await ReadAsync<BookmarkEntry>("bookmarks.json");
        var existing = items.FirstOrDefault(x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        if (existing != null) items.Remove(existing);
        else items.Insert(0, new BookmarkEntry(url, string.IsNullOrWhiteSpace(title) ? url : title, DateTimeOffset.Now));
        await WriteAsync("bookmarks.json", items.Take(500).ToList());
    }

    public Task<List<BookmarkEntry>> GetBookmarksAsync() => ReadAsync<BookmarkEntry>("bookmarks.json");

    public async Task AddPermissionAsync(string origin, string permission, string decision)
    {
        var items = await ReadAsync<PermissionEntry>("permissions.json");
        items.RemoveAll(x => x.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) && x.Permission.Equals(permission, StringComparison.OrdinalIgnoreCase));
        items.Insert(0, new PermissionEntry(origin, permission, decision, DateTimeOffset.Now));
        await WriteAsync("permissions.json", items.Take(300).ToList());
    }

    public Task<List<PermissionEntry>> GetPermissionsAsync() => ReadAsync<PermissionEntry>("permissions.json");

    private async Task<List<T>> ReadAsync<T>(string file)
    {
        try
        {
            var path = PathFor(file);
            if (!File.Exists(path)) return new List<T>();
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, _json) ?? new List<T>();
        }
        catch { return new List<T>(); }
    }

    private async Task WriteAsync<T>(string file, List<T> items)
    {
        try
        {
            var path = PathFor(file);
            var temp = path + ".tmp";
            await using (var stream = File.Create(temp)) await JsonSerializer.SerializeAsync(stream, items, _json);
            File.Move(temp, path, true);
        }
        catch { }
    }
}
