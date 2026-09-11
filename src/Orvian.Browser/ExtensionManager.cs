using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Orvian.Browser
{
    // ═══════════════════════════════════════════════════════════════
    //  ExtensionEntry — info about one loaded extension
    // ═══════════════════════════════════════════════════════════════
    public sealed class ExtensionEntry
    {
        public string  Id          { get; init; } = "";
        public string  Name        { get; init; } = "(unknown)";
        public string  Version     { get; init; } = "";
        public string  Description { get; init; } = "";
        public string  FolderPath  { get; init; } = "";
        public bool    IsEnabled   { get; set; }

        internal CoreWebView2BrowserExtension? Native { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ExtensionManager — loads, enables, disables and removes
    //  Chrome-compatible (Manifest V2/V3) extensions via WebView2.
    //
    //  Extensions live in:  <AppDir>\extensions\<ExtensionName>\
    //  Each folder must contain a valid manifest.json.
    //
    //  Usage:
    //    await ExtensionManager.InitAsync(core);
    //    await ExtensionManager.InstallFromFolderAsync(core, path);
    // ═══════════════════════════════════════════════════════════════
    public static class ExtensionManager
    {
        // ── Extension root folder ──────────────────────────────────
        public static string ExtensionsRoot { get; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "extensions");

        // ── Live collection of loaded extensions ───────────────────
        private static readonly List<ExtensionEntry> _list = new();
        public  static ReadOnlyCollection<ExtensionEntry> All => _list.AsReadOnly();

        // ══════════════════════════════════════════════════════════
        //  Init — call once after CoreWebView2 is ready
        // ══════════════════════════════════════════════════════════
        public static async Task InitAsync(CoreWebView2 core)
        {
            // 1. Apply preferred colour scheme (dark)
            core.Profile.PreferredColorScheme =
                CoreWebView2PreferredColorScheme.Dark;

            // 2. Pre-load any extension folders that already exist
            await LoadFromFolderAsync(core, ExtensionsRoot);

            System.Diagnostics.Debug.WriteLine(
                $"[ExtMgr] {_list.Count} extension(s) loaded from {ExtensionsRoot}");
        }

        // ══════════════════════════════════════════════════════════
        //  Load all extensions in a directory (silent, no-throw)
        // ══════════════════════════════════════════════════════════
        private static async Task LoadFromFolderAsync(CoreWebView2 core, string dir)
        {
            if (!Directory.Exists(dir)) return;

            foreach (var folder in Directory.GetDirectories(dir))
            {
                var manifest = Path.Combine(folder, "manifest.json");
                if (!File.Exists(manifest)) continue;
                if (_list.Any(e => e.FolderPath.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                    continue;   // already loaded

                await TryLoadOneAsync(core, folder);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  InstallFromFolderAsync — drag-drop or "Load unpacked"
        //  Returns the entry on success, null on failure.
        // ══════════════════════════════════════════════════════════
        public static async Task<ExtensionEntry?> InstallFromFolderAsync(
            CoreWebView2 core, string sourceFolder)
        {
            var manifest = Path.Combine(sourceFolder, "manifest.json");
            if (!File.Exists(manifest))
                throw new FileNotFoundException(
                    "Kein manifest.json im gewählten Ordner.", manifest);

            // Copy to managed extensions directory so it persists
            var name = Path.GetFileName(sourceFolder.TrimEnd('/', '\\'));
            var dest = Path.Combine(ExtensionsRoot, name);
            if (!sourceFolder.Equals(dest, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
                CopyDirectory(sourceFolder, dest);
            }

            return await TryLoadOneAsync(core, dest);
        }

        // ══════════════════════════════════════════════════════════
        //  Enable / Disable / Remove
        // ══════════════════════════════════════════════════════════
        public static async Task EnableAsync(string id)
        {
            var e = Find(id);
            if (e?.Native is null) return;
            e.Native.IsEnabled = true;
            e.IsEnabled = true;
        }

        public static async Task DisableAsync(string id)
        {
            var e = Find(id);
            if (e?.Native is null) return;
            e.Native.IsEnabled = false;
            e.IsEnabled = false;
        }

        public static async Task RemoveAsync(string id)
        {
            var e = Find(id);
            if (e is null) return;

            if (e.Native is not null)
                await e.Native.RemoveAsync();

            _list.Remove(e);

            // Remove from disk
            if (Directory.Exists(e.FolderPath))
            {
                try { Directory.Delete(e.FolderPath, recursive: true); }
                catch { /* ignore – may be locked */ }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  Toggle helper (for UI checkboxes)
        // ══════════════════════════════════════════════════════════
        public static Task ToggleAsync(string id) =>
            Find(id)?.IsEnabled == true
                ? DisableAsync(id)
                : EnableAsync(id);

        // ══════════════════════════════════════════════════════════
        //  Open the extensions folder in Explorer
        // ══════════════════════════════════════════════════════════
        public static void OpenExtensionsFolder()
        {
            Directory.CreateDirectory(ExtensionsRoot);
            System.Diagnostics.Process.Start("explorer.exe", ExtensionsRoot);
        }

        // ══════════════════════════════════════════════════════════
        //  Internal helpers
        // ══════════════════════════════════════════════════════════
        private static async Task<ExtensionEntry?> TryLoadOneAsync(
            CoreWebView2 core, string folder)
        {
            try
            {
                var native = await core.Profile.AddBrowserExtensionAsync(folder);
                native.IsEnabled = true;

                var (name, version, description) = ReadManifest(folder);

                var entry = new ExtensionEntry
                {
                    Id          = native.Id,
                    Name        = string.IsNullOrWhiteSpace(name) ? native.Name : name,
                    Version     = version,
                    Description = description,
                    FolderPath  = folder,
                    IsEnabled   = true,
                    Native      = native,
                };
                _list.Add(entry);
                System.Diagnostics.Debug.WriteLine($"[ExtMgr] ✓ loaded: {entry.Name} {entry.Version}");
                return entry;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ExtMgr] ✗ failed to load '{folder}': {ex.Message}");
                return null;
            }
        }

        private static (string name, string version, string description)
            ReadManifest(string folder)
        {
            var path = Path.Combine(folder, "manifest.json");
            if (!File.Exists(path)) return ("", "", "");
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                var n = root.TryGetProperty("name",        out var np) ? np.GetString() ?? "" : "";
                var v = root.TryGetProperty("version",     out var vp) ? vp.GetString() ?? "" : "";
                var d = root.TryGetProperty("description", out var dp) ? dp.GetString() ?? "" : "";
                // Strip Chrome i18n placeholders like "__MSG_extName__"
                if (n.StartsWith("__MSG_")) n = Path.GetFileName(folder);
                return (n, v, d);
            }
            catch { return ("", "", ""); }
        }

        private static ExtensionEntry? Find(string id) =>
            _list.FirstOrDefault(e => e.Id == id);

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
            foreach (var sub in Directory.GetDirectories(src))
                CopyDirectory(sub, Path.Combine(dst, Path.GetFileName(sub)));
        }
    }
}
