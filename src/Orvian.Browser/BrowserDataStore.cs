using System.Text.Json;

namespace Orvian.Browser;

public sealed record HistoryEntry(string Url, string Title, DateTimeOffset VisitedAt);
public sealed record BookmarkEntry(string Url, string Title, DateTimeOffset AddedAt);
public sealed record PermissionEntry(string Origin, string Permission, string Decision, DateTimeOffset DecidedAt);

public sealed class BrowserDataStore
{
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orvian",
        "Data");

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public BrowserDataStore()
        => Directory.CreateDirectory(_root);

    private string PathFor(string name)
        => Path.Combine(_root, name);

    public async Task AddHistoryAsync(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return;

        await _ioLock.WaitAsync();
        try
        {
            var items = await ReadUnlockedAsync<HistoryEntry>("history.json");
            items.Insert(
                0,
                new HistoryEntry(
                    url,
                    string.IsNullOrWhiteSpace(title) ? url : title,
                    DateTimeOffset.Now));

            var unique = items
                .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(500)
                .ToList();

            await WriteUnlockedAsync("history.json", unique);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<List<HistoryEntry>> GetHistoryAsync()
        => await ReadLockedAsync<HistoryEntry>("history.json");

    public async Task ToggleBookmarkAsync(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        await _ioLock.WaitAsync();
        try
        {
            var items = await ReadUnlockedAsync<BookmarkEntry>("bookmarks.json");
            var existing = items.FirstOrDefault(
                x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                items.Remove(existing);
            else
                items.Insert(
                    0,
                    new BookmarkEntry(
                        url,
                        string.IsNullOrWhiteSpace(title) ? url : title,
                        DateTimeOffset.Now));

            await WriteUnlockedAsync("bookmarks.json", items.Take(500).ToList());
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<List<BookmarkEntry>> GetBookmarksAsync()
        => await ReadLockedAsync<BookmarkEntry>("bookmarks.json");

    public async Task AddPermissionAsync(string origin, string permission, string decision)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(permission))
            return;

        await _ioLock.WaitAsync();
        try
        {
            var items = await ReadUnlockedAsync<PermissionEntry>("permissions.json");
            items.RemoveAll(
                x => x.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase) &&
                     x.Permission.Equals(permission, StringComparison.OrdinalIgnoreCase));

            items.Insert(
                0,
                new PermissionEntry(
                    origin,
                    permission,
                    decision,
                    DateTimeOffset.Now));

            await WriteUnlockedAsync("permissions.json", items.Take(300).ToList());
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<List<PermissionEntry>> GetPermissionsAsync()
        => await ReadLockedAsync<PermissionEntry>("permissions.json");

    private async Task<List<T>> ReadLockedAsync<T>(string file)
    {
        await _ioLock.WaitAsync();
        try
        {
            return await ReadUnlockedAsync<T>(file);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<List<T>> ReadUnlockedAsync<T>(string file)
    {
        try
        {
            var path = PathFor(file);
            if (!File.Exists(path))
                return new List<T>();

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<List<T>>(stream, _json)
                   ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private async Task WriteUnlockedAsync<T>(string file, List<T> items)
    {
        try
        {
            var path = PathFor(file);
            var temp = path + ".tmp";

            await using (var stream = new FileStream(
                temp,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, items, _json);
                await stream.FlushAsync();
            }

            File.Move(temp, path, true);
        }
        catch
        {
            try
            {
                var temp = PathFor(file) + ".tmp";
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch { }
        }
    }
}
