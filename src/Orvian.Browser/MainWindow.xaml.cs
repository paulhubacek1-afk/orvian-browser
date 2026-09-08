using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Text.Json;
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
        public TextBlock? TitleBlock { get; init; }
        public string Title { get; set; } = "Neuer Tab";
        public string Address { get; set; } = "orvian://newtab";
    }

    private readonly List<BrowserTab> _tabs = new();
    private readonly List<string> _history = new();
    private CoreWebView2Environment? _environment;
    private BrowserTab? _activeTab;
    private bool _ready;

    private static string UserDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orvian", "WebView2");

    private const string NewTabHtml = """
<!doctype html>
<html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Neuer Tab</title>
<style>
:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Arial,sans-serif;color:#202124;background:#fff;min-height:100vh;display:flex;justify-content:center}main{width:min(760px,90vw);padding-top:17vh;text-align:center}.logo{font-size:42px;font-weight:500;letter-spacing:-2px;margin-bottom:28px}.logo b{color:#1a73e8}.search{height:48px;border:1px solid #dfe1e5;border-radius:24px;display:flex;align-items:center;padding:0 14px;box-shadow:0 1px 5px #00000018}.search:focus-within{box-shadow:0 1px 6px #00000028;border-color:#dfe1e5}.search span{font-size:19px;color:#5f6368;margin-right:10px}.search input{border:0;outline:0;flex:1;font-size:16px;background:transparent}.tiles{display:flex;justify-content:center;gap:14px;margin-top:28px;flex-wrap:wrap}.tile{width:110px;height:94px;border:0;border-radius:8px;background:#fff;cursor:pointer;padding:14px}.tile:hover{background:#f1f3f4}.tile .icon{width:44px;height:44px;margin:auto;border-radius:50%;background:#f1f3f4;display:grid;place-items:center;font-size:20px}.tile small{display:block;margin-top:9px;color:#3c4043}
</style></head><body><main><div class="logo"><b>O</b>rvian</div><form id="f" class="search"><span>⌕</span><input id="q" autofocus autocomplete="off" placeholder="Suchen oder URL eingeben"></form><div class="tiles"><button class="tile" onclick="go('https://www.google.com/')"><div class="icon">G</div><small>Google</small></button><button class="tile" onclick="go('https://www.youtube.com/')"><div class="icon">▶</div><small>YouTube</small></button><button class="tile" onclick="go('https://github.com/')"><div class="icon">⌘</div><small>GitHub</small></button></div></main><script>const w=window.chrome?.webview;function go(u){w?.postMessage('go:'+u)}document.getElementById('f').onsubmit=e=>{e.preventDefault();const q=document.getElementById('q').value.trim();if(q)w?.postMessage('search:'+q)};</script></body></html>
""";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseDown += (_, _) => { if (MenuPopup.Visibility == Visibility.Visible) MenuPopup.Visibility = Visibility.Collapsed; };
        Closed += (_, _) => { foreach (var tab in _tabs) { try { tab.View.Dispose(); } catch { } } };
    }

    private async Task InitializeAsync()
    {
        if (_ready) return;
        try
        {
            Directory.CreateDirectory(UserDataFolder);
            var options = new CoreWebView2EnvironmentOptions(
                "--disable-features=msWebOOUI,msSmartScreenProtection --disable-background-networking");
            _environment = await CoreWebView2Environment.CreateAsync(null, UserDataFolder, options);
            _ready = true;
            await CreateTabAsync(true, null);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Die Chromium-WebView2-Engine konnte nicht gestartet werden.\n\n" + ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CreateTabAsync(bool select, string? url)
    {
        if (!_ready || _environment == null) return;
        var view = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var title = new TextBlock { Text = "Neuer Tab", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var close = new Button { Content = "×", Width = 26, Height = 26, Margin = new Thickness(6, 0, 0, 0), FontSize = 16 };
        var panel = new Grid { Margin = new Thickness(2, 0, 2, 0) };
        panel.ColumnDefinitions.Add(new ColumnDefinition());
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(title, 0); Grid.SetColumn(close, 1); panel.Children.Add(title); panel.Children.Add(close);
        var header = new Button { Style = (Style)FindResource("TabButton"), Content = panel };
        var tab = new BrowserTab { View = view, Header = header, TitleBlock = title };
        header.Tag = tab;
        close.Tag = tab;
        header.Click += (_, e) => { if (e.OriginalSource != close) SelectTab(tab); };
        close.Click += (_, e) => { e.Handled = true; CloseTab(tab); };
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
                Dispatcher.InvokeAsync(async () => await CreateTabAsync(true, e.Uri));
            };
            if (select) SelectTab(tab);
            if (string.IsNullOrWhiteSpace(url)) await NavigateNewTabAsync(tab);
            else Navigate(tab, url);
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
                if (string.IsNullOrWhiteSpace(name)) name = "download";
                e.ResultFilePath = Path.Combine(downloads, name);
            }
            catch { }
        };
        core.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
                e.Cancel = true;
        };
        core.SourceChanged += (_, _) => UpdateAddressAndNavigation();
        core.DocumentTitleChanged += (_, _) =>
        {
            if (_activeTab?.View.CoreWebView2 == core) UpdateTabTitle(_activeTab, core.DocumentTitle);
        };
        core.HistoryChanged += (_, _) => UpdateNavigationUi();
    }

    private async Task NavigateNewTabAsync(BrowserTab tab)
    {
        tab.Address = "orvian://newtab";
        tab.View.CoreWebView2.NavigateToString(NewTabHtml);
        await Task.CompletedTask;
    }

    private void HandleNewTabMessage(BrowserTab tab, string message)
    {
        if (message.StartsWith("go:", StringComparison.Ordinal)) Navigate(tab, message[3..]);
        else if (message.StartsWith("search:", StringComparison.Ordinal)) Navigate(tab, BuildSearchUrl(message[7..]));
    }

    private static string BuildSearchUrl(string query) => "https://www.google.com/search?q=" + Uri.EscapeDataString(query);

    private void Navigate(BrowserTab tab, string input)
    {
        input = input.Trim();
        if (input.Length == 0) return;
        string url;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            url = uri.ToString();
        else if (input.Contains('.') && !input.Contains(' '))
            url = "https://" + input;
        else
            url = BuildSearchUrl(input);
        tab.Address = url;
        tab.View.CoreWebView2.Navigate(url);
        AddHistory(url);
    }

    private void SelectTab(BrowserTab tab)
    {
        if (!_tabs.Contains(tab)) return;
        _activeTab = tab;
        BrowserHost.Content = tab.View;
        AddressBox.Text = tab.Address == "orvian://newtab" ? "" : tab.Address;
        UpdateTabVisuals();
        UpdateNavigationUi();
        tab.View.Focus();
    }

    private void UpdateTabVisuals()
    {
        foreach (var tab in _tabs)
        {
            tab.Header.Background = tab == _activeTab ? (Brush)FindResource("TabActive") : (Brush)FindResource("TabInactive");
            tab.Header.Foreground = tab == _activeTab ? (Brush)FindResource("Text") : (Brush)FindResource("Muted");
        }
    }

    private static void UpdateTabTitle(BrowserTab tab, string? title)
    {
        tab.Title = string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title.Trim();
        if (tab.TitleBlock != null) tab.TitleBlock.Text = tab.Title.Length > 34 ? tab.Title[..34] + "…" : tab.Title;
    }

    private void UpdateAddressAndNavigation()
    {
        if (_activeTab?.View.CoreWebView2 == null) return;
        var source = _activeTab.View.CoreWebView2.Source;
        if (!string.IsNullOrWhiteSpace(source) && !source.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            _activeTab.Address = source;
            if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) AddressBox.Text = source;
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
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;
        _history.Remove(url);
        _history.Insert(0, url);
        if (_history.Count > 500) _history.RemoveAt(_history.Count - 1);
    }

    private void CloseTab(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0) return;
        var active = tab == _activeTab;
        _tabs.RemoveAt(index);
        TabStrip.Children.Remove(tab.Header);
        try { tab.View.Dispose(); } catch { }
        if (_tabs.Count == 0) { _ = CreateTabAsync(true, null); return; }
        if (active) SelectTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync(true, null);
    private void Back_Click(object sender, RoutedEventArgs e) { if (_activeTab?.View.CoreWebView2?.CanGoBack == true) _activeTab.View.CoreWebView2.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (_activeTab?.View.CoreWebView2?.CanGoForward == true) _activeTab.View.CoreWebView2.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) { _activeTab?.View.CoreWebView2?.Reload(); }
    private void Bookmark_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Lesezeichen werden in der nächsten Ausbaustufe persistent gespeichert.", "Orvian", MessageBoxButton.OK, MessageBoxImage.Information);
    private void Security_Click(object sender, RoutedEventArgs e)
    {
        var core = _activeTab?.View.CoreWebView2;
        if (core == null) return;
        MessageBox.Show(this, $"Verbindung\n\n{core.Source}\n\nHTTPS: {core.Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)}", "Websiteinformationen", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Menu_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = MenuPopup.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
    private async void MenuNewTab_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; await CreateTabAsync(true, null); }
    private void MenuNewWindow_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true }); }
    private void MenuHistory_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; MessageBox.Show(this, _history.Count == 0 ? "Noch kein Verlauf." : string.Join("\n", _history.Take(30)), "Verlauf", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void MenuDownloads_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; Process.Start(new ProcessStartInfo("explorer.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")) { UseShellExecute = true }); }
    private void MenuBookmarks_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; MessageBox.Show(this, "Noch keine Lesezeichen gespeichert.", "Lesezeichen", MessageBoxButton.OK, MessageBoxImage.Information); }
    private void MenuSettings_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; MessageBox.Show(this, "Orvian verwendet die Chromium/WebView2-Engine.\n\nProfilordner:\n" + UserDataFolder, "Einstellungen", MessageBoxButton.OK, MessageBoxImage.Information); }
    private async void MenuClearData_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.Visibility = Visibility.Collapsed;
        if (MessageBox.Show(this, "Browserdaten dieses Orvian-Profils löschen?", "Browserdaten löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _history.Clear();
        try
        {
            if (_activeTab?.View.CoreWebView2 != null)
                await _activeTab.View.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.All);
            MessageBox.Show(this, "Browserdaten wurden gelöscht.", "Orvian", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void MenuAbout_Click(object sender, RoutedEventArgs e) { MenuPopup.Visibility = Visibility.Collapsed; MessageBox.Show(this, "Orvian Browser\nChromium/WebView2-basierte Browserengine", "Über Orvian", MessageBoxButton.OK, MessageBoxImage.Information); }

    private void AddressBox_GotFocus(object sender, RoutedEventArgs e) => AddressBox.SelectAll();
    private void AddressBox_LostFocus(object sender, RoutedEventArgs e) { }
    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _activeTab != null) { Navigate(_activeTab, AddressBox.Text); _activeTab.View.Focus(); e.Handled = true; }
        else if (e.Key == Key.Escape) { AddressBox.Text = _activeTab?.Address ?? ""; _activeTab?.View.Focus(); e.Handled = true; }
    }

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
        { await CreateTabAsync(true, null); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L)
        { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R)
        { _activeTab?.View.CoreWebView2?.Reload(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
        { if (_activeTab != null) CloseTab(_activeTab); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.H)
        { MenuHistory_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.J)
        { MenuDownloads_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left) Back_Click(this, new RoutedEventArgs());
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right) Forward_Click(this, new RoutedEventArgs());
    }
}