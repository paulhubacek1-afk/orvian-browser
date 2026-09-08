using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Orvian.Browser;

public partial class MainWindow : Window
{
    private sealed class BrowserTab
    {
        public required WebView2 View { get; init; }
        public required Button Header { get; init; }
        public required TextBlock TitleBlock { get; init; }
        public string Title { get; set; } = "Neuer Tab";
        public string Address { get; set; } = "orvian://newtab";
    }

    private sealed class BrowserProfile
    {
        public List<string> History { get; set; } = new();
        public List<string> Bookmarks { get; set; } = new();
    }

    private const string NewTabUrl = "orvian://newtab";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly List<BrowserTab> _tabs = new();
    private readonly BrowserProfile _profile = new();
    private CoreWebView2Environment? _environment;
    private BrowserTab? _activeTab;
    private bool _ready;

    private static string OrvianFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian");
    private static string UserDataFolder => Path.Combine(OrvianFolder, "WebView2");
    private static string ProfileFile => Path.Combine(OrvianFolder, "profile.json");

    private const string NewTabHtml = """
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Neuer Tab</title>
<style>
:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Arial,sans-serif;color:#17202a;background:#fff;min-height:100vh;display:flex;justify-content:center}main{width:min(760px,90vw);padding-top:17vh;text-align:center}.logo{font-size:44px;font-weight:600;letter-spacing:-2px;margin-bottom:28px}.logo b{color:#356ae6}.search{height:52px;border:1px solid #d9e0e8;border-radius:26px;display:flex;align-items:center;padding:0 16px;box-shadow:0 2px 8px #00000012}.search:focus-within{box-shadow:0 3px 12px #0000001c}.search span{font-size:19px;color:#667085;margin-right:10px}.search input{border:0;outline:0;flex:1;font-size:16px;background:transparent}.tiles{display:flex;justify-content:center;gap:14px;margin-top:28px;flex-wrap:wrap}.tile{width:112px;height:96px;border:0;border-radius:12px;background:#fff;cursor:pointer;padding:14px}.tile:hover{background:#f1f3f6}.tile .icon{width:44px;height:44px;margin:auto;border-radius:50%;background:#eef3fb;display:grid;place-items:center;font-size:20px}.tile small{display:block;margin-top:9px;color:#46505b}
</style>
</head>
<body>
<main>
<div class="logo"><b>O</b>rvian</div>
<form id="f" class="search"><span>⌕</span><input id="q" autofocus autocomplete="off" placeholder="Suchen oder URL eingeben"></form>
<div class="tiles">
<button class="tile" type="button" onclick="go('https://www.google.com/')"><div class="icon">G</div><small>Google</small></button>
<button class="tile" type="button" onclick="go('https://www.youtube.com/')"><div class="icon">▶</div><small>YouTube</small></button>
<button class="tile" type="button" onclick="go('https://github.com/')"><div class="icon">⌘</div><small>GitHub</small></button>
</div>
</main>
<script>
const w=window.chrome?.webview;
function go(u){w?.postMessage('go:'+u)}
document.getElementById('f').onsubmit=e=>{e.preventDefault();const q=document.getElementById('q').value.trim();if(q)w?.postMessage('search:'+q)};
</script>
</body>
</html>
""";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseDown += (_, _) =>
        {
            if (MenuPopup.Visibility == Visibility.Visible)
                MenuPopup.Visibility = Visibility.Collapsed;
        };
        Closed += (_, _) => DisposeTabs();
    }

    private async Task InitializeAsync()
    {
        if (_ready)
            return;

        try
        {
            Directory.CreateDirectory(OrvianFolder);
            Directory.CreateDirectory(UserDataFolder);
            LoadProfile();

            // WebView2 security features remain at their defaults. We deliberately do not disable SmartScreen,
            // safe browsing, networking protections, or other browser security controls.
            _environment = await CoreWebView2Environment.CreateAsync(null, UserDataFolder);
            _ready = true;
            await CreateTabAsync(true, null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Die WebView2-Engine konnte nicht gestartet werden.\n\n" + ex.Message,
                "Orvian",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task CreateTabAsync(bool select, string? url)
    {
        if (!_ready || _environment == null)
            return;

        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var title = new TextBlock
        {
            Text = "Neuer Tab",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var close = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 16
        };

        var panel = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        panel.ColumnDefinitions.Add(new ColumnDefinition());
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(title, 0);
        Grid.SetColumn(close, 1);
        panel.Children.Add(title);
        panel.Children.Add(close);

        var header = new Button
        {
            Style = (Style)FindResource("TabButton"),
            Content = panel
        };

        var tab = new BrowserTab
        {
            View = view,
            Header = header,
            TitleBlock = title
        };

        header.Tag = tab;
        close.Tag = tab;
        header.Click += (_, e) =>
        {
            if (e.OriginalSource != close)
                SelectTab(tab);
        };
        close.Click += (_, e) =>
        {
            e.Handled = true;
            CloseTab(tab);
        };

        TabStrip.Children.Add(header);
        _tabs.Add(tab);

        try
        {
            await view.EnsureCoreWebView2Async(_environment);
            ConfigureCore(view.CoreWebView2);
            view.CoreWebView2.WebMessageReceived += (_, e) => HandleNewTabMessage(tab, e.TryGetWebMessageAsString());
            view.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                var requestedUri = e.Uri;
                _ = Dispatcher.InvokeAsync(async () => await CreateTabAsync(true, requestedUri));
            };

            if (select)
                SelectTab(tab);

            if (string.IsNullOrWhiteSpace(url))
                await NavigateNewTabAsync(tab);
            else
                Navigate(tab, url);
        }
        catch
        {
            CloseTab(tab);
            throw;
        }
    }

    private void ConfigureCore(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsBuiltInErrorPageEnabled = true;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = true;

        core.DownloadStarting += (_, e) =>
        {
            try
            {
                var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloads);
                var name = Path.GetFileName(e.ResultFilePath);
                if (string.IsNullOrWhiteSpace(name))
                    name = "download";
                e.ResultFilePath = Path.Combine(downloads, name);
            }
            catch
            {
                // Keep WebView2's default path when the preferred path is unavailable.
            }
        };

        core.NavigationStarting += (_, e) =>
        {
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
                (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase) ||
                 uri.Scheme.Equals("javascript", StringComparison.OrdinalIgnoreCase)))
            {
                e.Cancel = true;
            }
        };

        core.SourceChanged += (_, _) => UpdateAddressAndNavigation();
        core.DocumentTitleChanged += (_, _) =>
        {
            if (_activeTab?.View.CoreWebView2 == core && _activeTab != null)
                UpdateTabTitle(_activeTab, core.DocumentTitle);
        };
        core.HistoryChanged += (_, _) => UpdateNavigationUi();
    }

    private async Task NavigateNewTabAsync(BrowserTab tab)
    {
        tab.Address = NewTabUrl;
        tab.View.CoreWebView2.NavigateToString(NewTabHtml);
        await Task.CompletedTask;
    }

    private void HandleNewTabMessage(BrowserTab tab, string message)
    {
        if (message.StartsWith("go:", StringComparison.Ordinal))
            Navigate(tab, message[3..]);
        else if (message.StartsWith("search:", StringComparison.Ordinal))
            Navigate(tab, BuildSearchUrl(message[7..]));
    }

    private static string BuildSearchUrl(string query) =>
        "https://www.google.com/search?q=" + Uri.EscapeDataString(query.Trim());

    private void Navigate(BrowserTab tab, string input)
    {
        input = input.Trim();
        if (input.Length == 0 || tab.View.CoreWebView2 == null)
            return;

        string url;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            url = uri.ToString();
        }
        else if (input.Contains('.') && !input.Contains(' '))
        {
            url = "https://" + input;
        }
        else
        {
            url = BuildSearchUrl(input);
        }

        tab.Address = url;
        tab.View.CoreWebView2.Navigate(url);
        AddHistory(url);
    }

    private void SelectTab(BrowserTab tab)
    {
        if (!_tabs.Contains(tab))
            return;

        _activeTab = tab;
        BrowserHost.Content = tab.View;
        AddressBox.Text = tab.Address == NewTabUrl ? string.Empty : tab.Address;
        UpdateTabVisuals();
        UpdateNavigationUi();
        tab.View.Focus();
    }

    private void UpdateTabVisuals()
    {
        foreach (var tab in _tabs)
        {
            tab.Header.Background = tab == _activeTab
                ? (Brush)FindResource("TabActive")
                : (Brush)FindResource("TabInactive");
            tab.Header.Foreground = tab == _activeTab
                ? (Brush)FindResource("Text")
                : (Brush)FindResource("Muted");
        }
    }

    private static void UpdateTabTitle(BrowserTab tab, string? title)
    {
        tab.Title = string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title.Trim();
        tab.TitleBlock.Text = tab.Title.Length > 34 ? tab.Title[..34] + "…" : tab.Title;
    }

    private void UpdateAddressAndNavigation()
    {
        if (_activeTab?.View.CoreWebView2 == null)
            return;

        var source = _activeTab.View.CoreWebView2.Source;
        if (!string.IsNullOrWhiteSpace(source) && !source.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            _activeTab.Address = source;
            if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                AddressBox.Text = source;
        }

        UpdateNavigationUi();
    }

    private void UpdateNavigationUi()
    {
        var core = _activeTab?.View.CoreWebView2;
        BackButton.IsEnabled = core?.CanGoBack == true;
        ForwardButton.IsEnabled = core?.CanGoForward == true;
    }

    private void AddHistory(string url)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return;

        _profile.History.Remove(url);
        _profile.History.Insert(0, url);
        if (_profile.History.Count > 500)
            _profile.History.RemoveAt(_profile.History.Count - 1);
        SaveProfile();
    }

    private void CloseTab(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
            return;

        var wasActive = tab == _activeTab;
        _tabs.RemoveAt(index);
        TabStrip.Children.Remove(tab.Header);

        try { tab.View.Dispose(); } catch { }

        if (_tabs.Count == 0)
        {
            _ = CreateTabAsync(true, null);
            return;
        }

        if (wasActive)
            SelectTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
    }

    private void DisposeTabs()
    {
        SaveProfile();
        foreach (var tab in _tabs)
        {
            try { tab.View.Dispose(); } catch { }
        }
        _tabs.Clear();
    }

    private void LoadProfile()
    {
        try
        {
            if (!File.Exists(ProfileFile))
                return;

            var json = File.ReadAllText(ProfileFile);
            var saved = JsonSerializer.Deserialize<BrowserProfile>(json, JsonOptions);
            if (saved == null)
                return;

            _profile.History = saved.History.Distinct(StringComparer.OrdinalIgnoreCase).Take(500).ToList();
            _profile.Bookmarks = saved.Bookmarks.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            _profile.History.Clear();
            _profile.Bookmarks.Clear();
        }
    }

    private void SaveProfile()
    {
        try
        {
            Directory.CreateDirectory(OrvianFolder);
            File.WriteAllText(ProfileFile, JsonSerializer.Serialize(_profile, JsonOptions));
        }
        catch
        {
            // Browser functionality must not fail just because persistence is unavailable.
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => _ = CreateTabAsync(true, null);

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab?.View.CoreWebView2?.CanGoBack == true)
            _activeTab.View.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab?.View.CoreWebView2?.CanGoForward == true)
            _activeTab.View.CoreWebView2.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => _activeTab?.View.CoreWebView2?.Reload();

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _activeTab == null)
            return;

        Navigate(_activeTab, AddressBox.Text);
        e.Handled = true;
    }

    private void AddressBox_GotFocus(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(new Action(() => AddressBox.SelectAll()));
    private void AddressBox_LostFocus(object sender, RoutedEventArgs e) => UpdateAddressAndNavigation();

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab == null || !_activeTab.Address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return;

        var url = _activeTab.Address;
        if (_profile.Bookmarks.Contains(url, StringComparer.OrdinalIgnoreCase))
            _profile.Bookmarks.RemoveAll(x => x.Equals(url, StringComparison.OrdinalIgnoreCase));
        else
            _profile.Bookmarks.Insert(0, url);
        SaveProfile();
    }

    private void Security_Click(object sender, RoutedEventArgs e)
    {
        var url = _activeTab?.Address ?? string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Diese Seite verwendet HTTPS.\n\n" + uri.Host, "Verbindung sicher", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Diese Verbindung verwendet kein HTTPS. Übertrage dort keine sensiblen Daten.", "Nicht verschlüsselt", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MessageBox.Show(this, "Keine Website geöffnet.", "Orvian", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Menu_Click(object sender, RoutedEventArgs e) =>
        MenuPopup.Visibility = MenuPopup.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void MenuNewTab_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; _ = CreateTabAsync(true, null); }

    private void MenuNewWindow_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        var window = new MainWindow();
        window.Show();
    }

    private void MenuHistory_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        var items = _profile.History.Take(20).ToList();
        MessageBox.Show(this,
            items.Count == 0 ? "Noch kein Verlauf vorhanden." : string.Join(Environment.NewLine, items),
            "Verlauf",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuDownloads_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = downloads,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Downloads", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuBookmarks_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        var items = _profile.Bookmarks.Take(30).ToList();
        MessageBox.Show(this,
            items.Count == 0 ? "Noch keine Lesezeichen gespeichert." : string.Join(Environment.NewLine, items),
            "Lesezeichen",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        MessageBox.Show(this,
            "Orvian verwendet die WebView2-Standardsicherheit.\n\nPasswort-Autosave und allgemeines Autofill sind deaktiviert. Downloads werden im Windows-Downloads-Ordner gespeichert.",
            "Orvian Einstellungen",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void MenuClearData_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        if (_environment == null)
            return;

        var result = MessageBox.Show(this,
            "Verlauf, Cookies, Cache und Profildaten dieses Orvian-Profils löschen?",
            "Browserdaten löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            var profile = await _environment.CreateCoreWebView2ControllerOptionsAsync() is not null
                ? _activeTab?.View.CoreWebView2?.Profile
                : null;
            if (profile != null)
                await profile.ClearBrowsingDataAsync();

            _profile.History.Clear();
            _profile.Bookmarks.Clear();
            SaveProfile();
            MessageBox.Show(this, "Die Browserdaten wurden gelöscht.", "Orvian", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Browserdaten konnten nicht vollständig gelöscht werden.\n\n" + ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        MessageBox.Show(this,
            "Orvian Browser 2.0.0\n\nChromium-kompatible WebView2-Engine\nWindows x64\n\nSicherheitsfunktionen der Engine bleiben aktiviert.",
            "Über Orvian",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.T:
                    _ = CreateTabAsync(true, null);
                    e.Handled = true;
                    break;
                case Key.L:
                    AddressBox.Focus();
                    AddressBox.SelectAll();
                    e.Handled = true;
                    break;
                case Key.R:
                    _activeTab?.View.CoreWebView2?.Reload();
                    e.Handled = true;
                    break;
                case Key.W:
                    if (_activeTab != null)
                        CloseTab(_activeTab);
                    e.Handled = true;
                    break;
                case Key.H:
                    MenuHistory_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.J:
                    MenuDownloads_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (e.Key == Key.Left)
            {
                Back_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                Forward_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
