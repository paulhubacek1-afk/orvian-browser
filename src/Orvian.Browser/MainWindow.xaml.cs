using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow : Window
{
    private sealed class BrowserTab
    {
        public required WebView2 View { get; init; }
        public required Button HeaderButton { get; init; }
        public bool InternalPage { get; set; }
        public string Title { get; set; } = "Neuer Tab";
        public string DisplayAddress { get; set; } = "orvian://newtab";
    }

    private readonly Blocker _blocker = new();
    private readonly UpdateChecker _updateChecker = new();
    private readonly BrowserDataStore _data = new();
    private readonly PasswordVault _vault = new();
    private readonly List<BrowserTab> _tabs = new();
    private readonly List<(string FileName, string Path, DateTimeOffset StartedAt)> _downloads = new();

    private CoreWebView2Environment? _environment;
    private PandaControl? _panda;
    private DispatcherTimer? _updateTimer;
    private BrowserTab? _activeTab;
    private UpdateInfo? _pendingUpdate;
    private bool _browserReady;
    private bool _welcomeVisible;
    private bool _creatorBannerVisible;
    private bool _locked;
    private bool _loaded;

    private const string NewTabAddress = "orvian://newtab";
    private const string Noctra11TwitchUrl = "https://www.twitch.tv/noctra11";
    private const string Noctra11YouTubeUrl = "https://www.youtube.com/@Noctra11_Yt";
    private static readonly string WelcomeFlag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "welcome-shown.flag");

    private IReadOnlyList<BrowserCommand> Commands => new[]
    {
        new BrowserCommand("newtab", "Neuer Tab", "Neue Orvian-Startseite öffnen", "Ctrl+T"),
        new BrowserCommand("history", "Verlauf", "Zuletzt besuchte Seiten anzeigen", "Ctrl+H"),
        new BrowserCommand("bookmarks", "Lesezeichen", "Gespeicherte Seiten öffnen", "Ctrl+Shift+O"),
        new BrowserCommand("downloads", "Downloads", "Download-Verlauf anzeigen", "Ctrl+J"),
        new BrowserCommand("privacy", "Datenschutz-Center", "Schutzstatus und Blocker anzeigen", "Ctrl+Shift+P"),
        new BrowserCommand("settings", "Einstellungen", "Orvian konfigurieren", ""),
        new BrowserCommand("vault", "Passwort-Tresor", "Gespeicherte Passwörter verwalten", ""),
        new BrowserCommand("update", "Nach Updates suchen", "Remote nach einer neuen Orvian-Version suchen", ""),
        new BrowserCommand("reload", "Seite neu laden", "Aktuelle Seite neu laden", "Ctrl+R"),
        new BrowserCommand("focus", "Adressleiste", "URL oder Suchbegriff eingeben", "Ctrl+L")
    };

    private const string NewTabHtml = """
<!doctype html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Neuer Tab – Orvian</title>
<style>*{box-sizing:border-box}body{margin:0;min-height:100vh;font-family:Segoe UI,Arial,sans-serif;color:#172236;background:radial-gradient(circle at 80% 10%,rgba(100,165,255,.22),transparent 30%),linear-gradient(145deg,#fbfdff,#eef5ff)}main{max-width:1180px;margin:auto;padding:72px 54px}h1{font-size:58px;margin:12px 0}.brand{font-size:12px;letter-spacing:.22em;font-weight:800;color:#4169e1}.lead{font-size:19px;color:#66758c;max-width:720px}.search{display:flex;max-width:900px;padding:8px;background:#fff;border:1px solid #cbd8e8;border-radius:25px;box-shadow:0 20px 55px #2a46691f}input{flex:1;border:0;outline:0;font-size:17px;padding:15px}.go{width:58px;border:0;border-radius:18px;background:#4169e1;color:#fff;font-size:24px}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:15px;max-width:900px;margin-top:20px}.card{border:1px solid #d5e0ec;background:#ffffffd1;border-radius:20px;padding:22px;text-align:left;font-size:16px}.card span{display:block;margin-top:7px;color:#75839a;font-size:13px}</style></head>
<body><main><div class="brand">ORVIAN BROWSER</div><h1>Dein Browser. Dein Raum.</h1><p class="lead">Schnell, privat und auf Webkompatibilität ausgelegt – mit WebView2, echten Tabs und dem Orvian-Panda.</p><form id="search" class="search"><input id="q" autocomplete="off" autofocus placeholder="Suchen oder Website öffnen …"><button class="go">⌕</button></form><div class="grid"><button class="card" onclick="go('https://www.google.com/')"><b>Google</b><span>Web durchsuchen</span></button><button class="card" onclick="go('https://github.com/')"><b>GitHub</b><span>Code und Projekte</span></button><button class="card" onclick="go('https://www.youtube.com/')"><b>YouTube</b><span>Videos</span></button><button class="card" onclick="msg('privacy')"><b>Datenschutz</b><span>Schutzstatus</span></button><button class="card" onclick="msg('vault')"><b>Passwort-Tresor</b><span>Passwörter</span></button><button class="card" onclick="msg('settings')"><b>Einstellungen</b><span>Orvian konfigurieren</span></button></div></main><script>const w=window.chrome?.webview;function msg(x){w?.postMessage(x)}function go(x){location.href=x}document.getElementById('search').addEventListener('submit',e=>{e.preventDefault();const q=document.getElementById('q').value.trim();if(q)msg('search:'+q)})</script></body></html>
""";

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("Intro"));
        AddAnimatedPanda();
        ShowWelcomeIfNeeded();
        StartUpdateTimer();
        if (!_welcomeVisible) Dispatcher.BeginInvoke(new Action(ShowCreatorBanner), DispatcherPriority.ApplicationIdle);
        _ = InitializeBrowserAsync();
        _ = WarmupProtectionAsync();
        _ = CheckForUpdatesAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _updateTimer?.Stop();
        _panda?.StopAnimations();
        foreach (var tab in _tabs) { try { tab.View.Dispose(); } catch { } }
        _environment = null;
    }

    private void AddAnimatedPanda()
    {
        if (_panda != null) return;
        _panda = new PandaControl { Width = 112, Height = 112, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,0,18,18), Opacity = .94 };
        Panel.SetZIndex(_panda, 90);
        PandaLayer.Children.Add(_panda);
    }

    private void ShowWelcomeIfNeeded()
    {
        try
        {
            if (File.Exists(WelcomeFlag)) return;
            _welcomeVisible = true;
            WelcomeOverlay.Visibility = Visibility.Visible;
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("WelcomeIntro"));
            _panda?.Play(PandaMood.Happy);
        }
        catch { }
    }

    private void ShowCreatorBanner()
    {
        if (_creatorBannerVisible || _welcomeVisible || CreatorOverlay.Visibility == Visibility.Visible) return;
        _creatorBannerVisible = true;
        CreatorOverlay.Visibility = Visibility.Visible;
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("CreatorIntro"));
        _panda?.Play(PandaMood.Happy);
    }

    private async void CreatorBannerLater_Click(object sender, RoutedEventArgs e) => await CloseCreatorBannerAsync();

    private async void CreatorLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string url) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            await CloseCreatorBannerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Der Creator-Link konnte nicht geöffnet werden.\n\n" + ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task CloseCreatorBannerAsync()
    {
        if (!_creatorBannerVisible) return;
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("CreatorOutro"));
        _panda?.Play(PandaMood.Success);
        await Task.Delay(140);
        CreatorOverlay.Visibility = Visibility.Collapsed;
        _creatorBannerVisible = false;
        if (_pendingUpdate != null) ShowPendingUpdate();
    }

    private async void WelcomeContinue_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(WelcomeFlag)!); await File.WriteAllTextAsync(WelcomeFlag, DateTime.UtcNow.ToString("O")); } catch { }
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("WelcomeOutro"));
        _panda?.Play(PandaMood.Success);
        await Task.Delay(180);
        WelcomeOverlay.Visibility = Visibility.Collapsed;
        _welcomeVisible = false;
        ShowCreatorBanner();
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browserReady) return;
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "WebView2");
            Directory.CreateDirectory(folder);
            _environment = await CoreWebView2Environment.CreateAsync(null, folder, new CoreWebView2EnvironmentOptions());
            _browserReady = true;
            await AddTabAsync(true, null);
            _panda?.Play(PandaMood.Success);
        }
        catch (Exception ex)
        {
            _panda?.Play(PandaMood.Error);
            MessageBox.Show("Orvian konnte die WebView2-Browserengine nicht starten.\n\n" + ex.Message, "Orvian – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddTabAsync(bool select, string? initialUri)
    {
        if (!_browserReady || _environment == null) return;
        var view = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, DefaultBackgroundColor = System.Drawing.Color.White };
        var header = BuildTabHeader();
        var tab = new BrowserTab { View = view, HeaderButton = header };
        header.Tag = tab;
        HookWebView(view);
        _tabs.Add(tab);
        if (!TabStrip.Children.Contains(header)) TabStrip.Children.Add(header);
        try
        {
            await view.EnsureCoreWebView2Async(_environment);
            ConfigureCore(view.CoreWebView2);
            if (select) SelectTab(tab);
            if (string.IsNullOrWhiteSpace(initialUri)) await NavigateInternalAsync(tab, NewTabAddress, "Neuer Tab", NewTabHtml);
            else NavigateExternal(tab, initialUri);
        }
        catch (Exception ex)
        {
            _tabs.Remove(tab);
            TabStrip.Children.Remove(header);
            try { view.Dispose(); } catch { }
            MessageBox.Show("Der neue Tab konnte nicht gestartet werden.\n\n" + ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Button BuildTabHeader()
    {
        var close = new Button { Content = "×", Width = 26, Height = 26, Padding = new Thickness(0), Margin = new Thickness(5,0,0,0), ToolTip = "Tab schließen" };
        close.Click += CloseTab_Click;
        var title = new TextBlock { Text = "Neuer Tab", Tag = "tab-title", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeights.SemiBold };
        var icon = new Border { Width = 21, Height = 21, CornerRadius = new CornerRadius(11), Background = (Brush)FindResource("AccentBrush"), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "O", Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(title, 1);
        Grid.SetColumn(close, 2);
        layout.Children.Add(icon);
        layout.Children.Add(title);
        layout.Children.Add(close);
        var button = new Button { Style = (Style)FindResource("TabButton"), Content = layout };
        button.Click += TabHeader_Click;
        return button;
    }

    private void TabHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is BrowserTab tab) { SelectTab(tab); e.Handled = true; }
    }

    private void SelectTab(BrowserTab tab)
    {
        if (!_tabs.Contains(tab)) return;
        _activeTab = tab;
        BrowserHost.Content = tab.View;
        AddressBox.Text = tab.DisplayAddress;
        UpdateTabVisuals();
        UpdateNavigationUi();
        tab.View.Focus();
    }

    private void UpdateTabVisuals()
    {
        foreach (var tab in _tabs)
        {
            tab.HeaderButton.Background = tab == _activeTab ? (Brush)FindResource("AccentSoftBrush") : (Brush)FindResource("SurfaceBrush");
            tab.HeaderButton.Foreground = tab == _activeTab ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("MutedBrush");
            tab.HeaderButton.BorderBrush = tab == _activeTab ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("BorderBrush");
        }
    }

    private void UpdateTabTitle(BrowserTab tab, string title)
    {
        tab.Title = string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title.Trim();
        if (tab.HeaderButton.Content is Grid grid && grid.Children.OfType<TextBlock>().FirstOrDefault(x => Equals(x.Tag, "tab-title")) is { } titleBlock)
            titleBlock.Text = tab.Title;
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button close || close.Parent is not Grid grid || grid.Parent is not Button header || header.Tag is not BrowserTab tab) return;
        e.Handled = true;
        var index = _tabs.IndexOf(tab);
        if (index < 0) return;
        var active = tab == _activeTab;
        _tabs.RemoveAt(index);
        TabStrip.Children.Remove(tab.HeaderButton);
        try { tab.View.Dispose(); } catch { }
        if (_tabs.Count == 0) { _activeTab = null; _ = AddTabAsync(true, null); return; }
        if (active) SelectTab(_tabs[Math.Min(index, _tabs.Count - 1)]);
        else UpdateTabVisuals();
        _panda?.Play(PandaMood.Happy);
    }

    private void HookWebView(WebView2 view) => view.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;

    private void CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (sender is WebView2 view && e.IsSuccess && view.CoreWebView2 != null)
            ConfigureCore(view.CoreWebView2);
    }

    private void ConfigureCore(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        try { core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All); } catch { }
        core.WebResourceRequested -= WebResourceRequested; core.WebResourceRequested += WebResourceRequested;
        core.NavigationStarting -= NavigationStarting; core.NavigationStarting += NavigationStarting;
        core.NavigationCompleted -= NavigationCompleted; core.NavigationCompleted += NavigationCompleted;
        core.NewWindowRequested -= Core_NewWindowRequested; core.NewWindowRequested += Core_NewWindowRequested;
        core.WebMessageReceived -= WebMessageReceived; core.WebMessageReceived += WebMessageReceived;
        core.DownloadStarting -= DownloadStarting; core.DownloadStarting += DownloadStarting;
        core.PermissionRequested -= PermissionRequested; core.PermissionRequested += PermissionRequested;
    }

    private BrowserTab? TabFor(object? sender) => sender switch
    {
        CoreWebView2 core => _tabs.FirstOrDefault(x => ReferenceEquals(x.View.CoreWebView2, core)),
        WebView2 view => _tabs.FirstOrDefault(x => ReferenceEquals(x.View, view)),
        _ => null
    };

    private WebView2? CurrentView() => _activeTab?.View;

    private async Task WarmupProtectionAsync()
    {
        try
        {
            await _blocker.RefreshFiltersAsync();
            if (_browserReady)
                Dispatcher.Invoke(() => PrivacyButton.ToolTip = $"Datenschutz-Center • {_blocker.RuleCount:N0} Schutzregeln");
        }
        catch { }
    }

    private void StartUpdateTimer()
    {
        _updateTimer?.Stop();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await _updateChecker.GetLatestAsync();
            if (update == null || update.Version <= _updateChecker.CurrentVersion) return;
            _pendingUpdate = update;
            if (!_welcomeVisible && !_creatorBannerVisible && UpdateOverlay.Visibility != Visibility.Visible) ShowPendingUpdate();
        }
        catch { }
    }

    private void ShowPendingUpdate()
    {
        if (_pendingUpdate == null) return;
        UpdateText.Text = $"Orvian {_updateChecker.CurrentVersion} ist installiert. Version {_pendingUpdate.Version} ist verfügbar.";
        UpdateNowButton.Content = _pendingUpdate.HasInstaller ? "Jetzt aktualisieren" : "Release öffnen";
        UpdateOverlay.Visibility = Visibility.Visible;
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("UpdateIntro"));
        _panda?.Play(PandaMood.Update);
    }

    private void UpdateLater_Click(object sender, RoutedEventArgs e)
    {
        UpdateOverlay.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
        _panda?.StartIdle();
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;
        if (!_pendingUpdate.HasInstaller)
        {
            try { Process.Start(new ProcessStartInfo(UpdateChecker.ReleasesPage) { UseShellExecute = true }); } catch { }
            return;
        }
        UpdateNowButton.IsEnabled = false;
        try
        {
            var path = await _updateChecker.DownloadInstallerAsync(_pendingUpdate);
            if (path == null) throw new InvalidOperationException("Installer konnte nicht heruntergeladen werden.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateNowButton.IsEnabled = true;
            MessageBox.Show("Das Update konnte nicht gestartet werden.\n\n" + ex.Message, "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(e.Uri)) _ = AddTabAsync(true, e.Uri);
    }

    private void WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            if (e.ResourceContext == CoreWebView2WebResourceContext.Document || !_blocker.ShouldBlock(e.Request.Uri)) return;
            if (TabFor(sender)?.View.CoreWebView2 is { } core)
                e.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked by Orvian", "Content-Type: text/plain; charset=utf-8");
        }
        catch { }
    }

    private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message;
        try { message = e.TryGetWebMessageAsString(); } catch { return; }
        try
        {
            if (message.StartsWith("search:", StringComparison.Ordinal)) { NavigateFromAddressValue(message[7..]); return; }
            switch (message)
            {
                case "settings": Menu_Click(this, new RoutedEventArgs()); break;
                case "vault": OpenVault(); break;
                case "privacy": OpenPrivacyPage(); break;
                case "newtab": _ = AddTabAsync(true, null); break;
            }
        }
        catch { }
    }

    private void DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(folder);
            var file = Path.GetFileName(e.ResultFilePath);
            if (string.IsNullOrWhiteSpace(file)) file = "Orvian-Download";
            var target = Path.Combine(folder, file);
            var unique = target;
            var n = 2;
            while (File.Exists(unique)) unique = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(file)} ({n++}){Path.GetExtension(file)}");
            e.ResultFilePath = unique;
            _downloads.Insert(0, (Path.GetFileName(unique), unique, DateTimeOffset.Now));
            _panda?.Play(PandaMood.Happy);
        }
        catch { }
    }

    private async void PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        try
        {
            var allow = MessageBox.Show($"{e.Uri}\n\nDie Website möchte Zugriff auf: {e.PermissionKind}.\n\nZugriff erlauben?", "Orvian Berechtigung", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            e.State = allow ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            await _data.AddPermissionAsync(e.Uri, e.PermissionKind.ToString(), allow ? "Erlaubt" : "Abgelehnt");
        }
        catch { e.State = CoreWebView2PermissionState.Deny; }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var tab = TabFor(sender);
        if (tab == null) return;
        if (!(tab.InternalPage && e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)))
        {
            tab.InternalPage = false;
            tab.DisplayAddress = e.Uri;
            if (tab == _activeTab) AddressBox.Text = e.Uri;
        }
        _panda?.Play(PandaMood.Loading);
    }

    private async void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var tab = TabFor(sender);
        if (tab == null) return;
        if (!e.IsSuccess)
        {
            _panda?.Play(PandaMood.Error);
            return;
        }
        UpdateNavigationUi();
        if (!tab.InternalPage)
        {
            var title = tab.View.CoreWebView2.DocumentTitle;
            UpdateTabTitle(tab, string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title);
            if (tab.View.Source != null)
                try { await _data.AddHistoryAsync(tab.DisplayAddress, tab.Title); } catch { }
        }
        _panda?.Play(PandaMood.Success);
    }

    private void UpdateNavigationUi()
    {
        if (_activeTab == null) return;
        AddressBox.Text = _activeTab.DisplayAddress;
        BackButton.IsEnabled = _activeTab.View.CanGoBack;
        ForwardButton.IsEnabled = _activeTab.View.CanGoForward;
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _browserReady)
        {
            NavigateFromAddressValue(AddressBox.Text);
            e.Handled = true;
        }
    }

    private void NavigateFromAddressValue(string raw)
    {
        var value = raw.Trim();
        if (string.IsNullOrWhiteSpace(value) || _activeTab?.View.CoreWebView2 == null) return;
        if (value.Equals(NewTabAddress, StringComparison.OrdinalIgnoreCase))
        {
            _ = NavigateInternalAsync(_activeTab, NewTabAddress, "Neuer Tab", NewTabHtml);
            return;
        }
        if (Path.IsPathRooted(value))
        {
            try
            {
                var full = Path.GetFullPath(value);
                if (File.Exists(full))
                {
                    NavigateExternal(_activeTab, new Uri(full).AbsoluteUri);
                    return;
                }
            }
            catch { }
        }
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && IsAllowedNavigationScheme(absolute.Scheme))
        {
            NavigateExternal(_activeTab, value);
            return;
        }
        _activeTab.View.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    private static bool IsAllowedNavigationScheme(string scheme) =>
        scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals("file", StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals("data", StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals("about", StringComparison.OrdinalIgnoreCase);

    private void NavigateExternal(BrowserTab tab, string uri)
    {
        if (tab.View.CoreWebView2 == null) return;
        tab.InternalPage = false;
        tab.DisplayAddress = uri;
        if (tab == _activeTab) AddressBox.Text = uri;
        tab.View.CoreWebView2.Navigate(uri);
    }

    private Task NavigateInternalAsync(BrowserTab tab, string address, string title, string html)
    {
        if (tab.View.CoreWebView2 == null) return Task.CompletedTask;
        tab.InternalPage = true;
        tab.DisplayAddress = address;
        UpdateTabTitle(tab, title);
        if (tab == _activeTab) AddressBox.Text = address;
        tab.View.CoreWebView2.NavigateToString(html);
        return Task.CompletedTask;
    }

    private string HtmlPage(string title, string body) =>
        $$"""<!doctype html><html lang="de"><head><meta charset="utf-8"><title>{{WebUtility.HtmlEncode(title)}}</title><style>body{font-family:Segoe UI,Arial;background:linear-gradient(135deg,#f7f9fc,#edf5ff);color:#182235;padding:42px}.wrap{max-width:1080px;margin:auto}.row{display:block;padding:16px;background:#fff;border:1px solid #d8e2ed;border-radius:16px;margin:10px 0;text-decoration:none;color:#182235}.row span{display:block;color:#6d7a8e;font-size:13px;margin-top:5px}</style></head><body><div class="wrap">{{body}}</div></body></html>""";

    private async Task OpenHistoryPageAsync()
    {
        if (_activeTab == null) return;
        var rows = new StringBuilder();
        foreach (var x in await _data.GetHistoryAsync()) rows.Append($"<div class='row'><b>{WebUtility.HtmlEncode(x.Title)}</b><span>{WebUtility.HtmlEncode(x.Url)}</span></div>");
        await NavigateInternalAsync(_activeTab, "orvian://history", "Verlauf", HtmlPage("Verlauf", $"<h1>Verlauf</h1>{rows}"));
    }

    private async Task OpenBookmarksPageAsync()
    {
        if (_activeTab == null) return;
        var rows = new StringBuilder();
        foreach (var x in await _data.GetBookmarksAsync()) rows.Append($"<div class='row'><b>★ {WebUtility.HtmlEncode(x.Title)}</b><span>{WebUtility.HtmlEncode(x.Url)}</span></div>");
        await NavigateInternalAsync(_activeTab, "orvian://bookmarks", "Lesezeichen", HtmlPage("Lesezeichen", $"<h1>Lesezeichen</h1>{rows}"));
    }

    private void OpenDownloadsPage()
    {
        if (_activeTab == null) return;
        var rows = new StringBuilder();
        foreach (var x in _downloads) rows.Append($"<div class='row'><b>Download · {WebUtility.HtmlEncode(x.FileName)}</b><span>{WebUtility.HtmlEncode(x.Path)}</span></div>");
        _ = NavigateInternalAsync(_activeTab, "orvian://downloads", "Downloads", HtmlPage("Downloads", $"<h1>Downloads</h1>{rows}"));
    }

    private void OpenPrivacyPage()
    {
        if (_activeTab == null) return;
        _ = NavigateInternalAsync(_activeTab, "orvian://privacy", "Datenschutz", HtmlPage("Datenschutz", $"<h1>Datenschutz-Center</h1><p>{_blocker.RuleCount:N0} Schutzregeln geladen.</p>"));
    }

    private async Task OpenPermissionsPageAsync()
    {
        if (_activeTab == null) return;
        var rows = new StringBuilder();
        foreach (var x in await _data.GetPermissionsAsync()) rows.Append($"<div class='row'><b>{WebUtility.HtmlEncode(x.Origin)}</b><span>{WebUtility.HtmlEncode(x.Permission)} · {WebUtility.HtmlEncode(x.Decision)}</span></div>");
        await NavigateInternalAsync(_activeTab, "orvian://permissions", "Berechtigungen", HtmlPage("Berechtigungen", $"<h1>Berechtigungen</h1>{rows}"));
    }

    private void OpenVault() => new PasswordVaultWindow(_vault) { Owner = this }.ShowDialog();

    private void Back_Click(object sender, RoutedEventArgs e) { if (CurrentView()?.CanGoBack == true) CurrentView()!.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (CurrentView()?.CanGoForward == true) CurrentView()!.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => CurrentView()?.Reload();
    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab == null) return;
        _ = NavigateInternalAsync(_activeTab, NewTabAddress, "Neuer Tab", NewTabHtml);
    }
    private void NewTab_Click(object sender, RoutedEventArgs e) => _ = AddTabAsync(true, null);
    private async void Bookmark_Click(object sender, RoutedEventArgs e) => await Bookmark_ClickAsync();

    private async Task Bookmark_ClickAsync()
    {
        var v = CurrentView();
        if (v?.Source == null || _activeTab?.InternalPage == true) return;
        await _data.ToggleBookmarkAsync(v.Source.ToString(), v.CoreWebView2.DocumentTitle);
        _panda?.Play(PandaMood.Success);
    }

    private void Privacy_Click(object sender, RoutedEventArgs e) { OpenPrivacyPage(); _panda?.Play(PandaMood.Privacy); }
    private void Menu_Click(object sender, RoutedEventArgs e) => new SettingsWindow { Owner = this }.ShowDialog();

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.C) { LockBrowser(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.D) { UnlockBrowser(); e.Handled = true; return; }
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.K: ShowCommandPalette(); break;
            case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); break;
            case Key.T: _ = AddTabAsync(true, null); break;
            case Key.W: CloseActiveTab(); break;
            case Key.H: _ = OpenHistoryPageAsync(); break;
            case Key.J: OpenDownloadsPage(); break;
            case Key.R: CurrentView()?.Reload(); break;
            case Key.D: _ = Bookmark_ClickAsync(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void CloseActiveTab()
    {
        if (_activeTab?.HeaderButton.Content is not Grid grid) return;
        var close = grid.Children.OfType<Button>().FirstOrDefault(x => x.Content is string s && s == "×");
        if (close != null) CloseTab_Click(close, new RoutedEventArgs());
    }

    private void ShowCommandPalette()
    {
        var dialog = new CommandPaletteWindow(Commands) { Owner = this };
        dialog.CommandInvoked += (_, id) => Dispatcher.BeginInvoke(() =>
        {
            switch (id)
            {
                case "newtab": NewTab_Click(this, new RoutedEventArgs()); break;
                case "history": _ = OpenHistoryPageAsync(); break;
                case "downloads": OpenDownloadsPage(); break;
                case "privacy": OpenPrivacyPage(); break;
                case "vault": OpenVault(); break;
                case "update": _ = ForceUpdateCheckAsync(); break;
                case "reload": CurrentView()?.Reload(); break;
                case "focus": AddressBox.Focus(); AddressBox.SelectAll(); break;
                case "settings": Menu_Click(this, new RoutedEventArgs()); break;
            }
        });
        dialog.ShowDialog();
    }

    private async Task ForceUpdateCheckAsync()
    {
        try
        {
            var update = await _updateChecker.GetLatestAsync();
            if (update != null && update.Version > _updateChecker.CurrentVersion)
            {
                _pendingUpdate = update;
                ShowPendingUpdate();
            }
            else MessageBox.Show($"Du verwendest bereits Orvian {_updateChecker.CurrentVersion}.", "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Die Update-Prüfung ist momentan nicht erreichbar.\n\n" + ex.Message, "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LockBrowser()
    {
        if (_locked) return;
        _locked = true;
        BrowserHost.Visibility = Visibility.Hidden;
        LockOverlay.Visibility = Visibility.Visible;
        UnlockBox.Clear();
        UnlockBox.Focus();
        _panda?.Play(PandaMood.Privacy);
    }

    private void UnlockBrowser()
    {
        if (!_locked) return;
        _locked = false;
        LockOverlay.Visibility = Visibility.Collapsed;
        BrowserHost.Visibility = Visibility.Visible;
        _panda?.Play(PandaMood.Success);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => UnlockBrowser();
}