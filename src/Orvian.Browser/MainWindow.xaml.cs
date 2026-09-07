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
    private bool _locked;

    private const string NewTabAddress = "orvian://newtab";
    private static readonly string WelcomeFlag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "welcome-shown.flag");

    private IReadOnlyList<BrowserCommand> Commands => new[]
    {
        new BrowserCommand("newtab", "Neuer Tab", "Orvian-Startseite öffnen", "Ctrl+T"),
        new BrowserCommand("history", "Verlauf", "Zuletzt besuchte Seiten anzeigen", "Ctrl+H"),
        new BrowserCommand("bookmarks", "Lesezeichen", "Gespeicherte Seiten öffnen", "Ctrl+Shift+O"),
        new BrowserCommand("downloads", "Downloads", "Download-Verlauf anzeigen", "Ctrl+J"),
        new BrowserCommand("privacy", "Datenschutz-Center", "Schutzstatus und Blocker anzeigen", "Ctrl+Shift+P"),
        new BrowserCommand("permissions", "Berechtigungen", "Kamera, Mikrofon & Standort verwalten", ""),
        new BrowserCommand("settings", "Einstellungen", "Orvian konfigurieren", ""),
        new BrowserCommand("vault", "Passwort-Tresor", "Gespeicherte Passwörter verwalten", ""),
        new BrowserCommand("update", "Nach Updates suchen", "Orvian auf neue Version prüfen", ""),
        new BrowserCommand("reload", "Seite neu laden", "Aktuelle Seite neu laden", "Ctrl+R"),
        new BrowserCommand("focus", "Adressleiste", "URL oder Suche eingeben", "Ctrl+L")
    };

    private const string NewTabHtml = """
        <!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><style>
        *{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:radial-gradient(circle at 85% 12%,#dcecff 0,#f7faff 34%,#f1f5fb 100%);color:#172236;min-height:100vh}main{max-width:1120px;margin:0 auto;padding:62px 48px 70px}.hero{display:flex;gap:28px;align-items:center;margin:20px 0 34px}.panda{font-size:88px;filter:drop-shadow(0 16px 26px rgba(32,55,90,.16));animation:float 2.8s ease-in-out infinite}.eyebrow{font-size:12px;letter-spacing:.18em;font-weight:800;color:#2f6bff}.hero h1{font-size:44px;line-height:1.05;margin:7px 0 12px}.hero p{font-size:18px;color:#68768d;margin:0}.search{display:flex;max-width:820px;background:#fff;border:1px solid #ced9e7;border-radius:22px;padding:8px;box-shadow:0 15px 40px rgba(48,74,110,.12)}.search input{flex:1;border:0;outline:0;font-size:17px;padding:13px 15px;background:transparent}.search button{width:54px;border:0;border-radius:16px;background:#2f6bff;color:#fff;font-size:25px;cursor:pointer}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-top:24px}.card{border:1px solid #d8e2ee;background:rgba(255,255,255,.88);border-radius:19px;padding:21px;text-align:left;cursor:pointer;box-shadow:0 10px 28px rgba(40,63,95,.07);transition:transform .18s,box-shadow .18s}.card:hover{transform:translateY(-3px);box-shadow:0 16px 34px rgba(40,63,95,.12)}.card b,.card span{display:block}.card b{font-size:16px}.card span{color:#728096;font-size:13px;margin-top:6px}.tip{margin-top:24px;color:#7b879a;font-size:13px}@keyframes float{0%,100%{transform:translateY(0) rotate(-2deg)}50%{transform:translateY(-8px) rotate(2deg)}}
        </style></head><body><main><div class='hero'><div class='panda'>🐼</div><div><div class='eyebrow'>ORVIAN BROWSER</div><h1>Dein Browser. Dein Raum.</h1><p>Schnell, privat und mit einem Panda, der wirklich mitmacht.</p></div></div><form id='search' class='search'><input id='q' autocomplete='off' autofocus placeholder='Suchen oder Adresse eingeben …'/><button>⌕</button></form><div class='grid'><button class='card' onclick="go('https://www.google.com/')"><b>🔎 Google</b><span>Web durchsuchen</span></button><button class='card' onclick="go('https://github.com/')"><b>◆ GitHub</b><span>Code & Projekte</span></button><button class='card' onclick="go('https://www.youtube.com/')"><b>▶ YouTube</b><span>Videos</span></button><button class='card' onclick="msg('privacy')"><b>🛡 Datenschutz</b><span>Schutzstatus ansehen</span></button><button class='card' onclick="msg('vault')"><b>🔐 Passwort-Tresor</b><span>Passwörter sicher verwalten</span></button><button class='card' onclick="msg('settings')"><b>⚙ Einstellungen</b><span>Orvian personalisieren</span></button></div><div class='tip'>Tipp: <b>Ctrl + K</b> öffnet die Command Palette. Mit <b>Ctrl + T</b> bleibt dein aktueller Tab offen.</div></main><script>const w=window.chrome?.webview;function msg(x){w?.postMessage(x)}function go(x){location.href=x}document.getElementById('search').onsubmit=(e)=>{e.preventDefault();msg('search:'+document.getElementById('q').value)}</script></body></html>
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
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("Intro"));
        AddAnimatedPanda();
        ShowWelcomeIfNeeded();
        StartUpdateTimer();
        _ = InitializeBrowserAsync();
        _ = WarmupProtectionAsync();
        _ = CheckForUpdatesAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _updateTimer?.Stop();
        foreach (var tab in _tabs)
        {
            try { tab.View.Dispose(); } catch { }
        }
    }

    private void AddAnimatedPanda()
    {
        if (_panda != null) return;
        _panda = new PandaControl { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 24, 22) };
        Panel.SetZIndex(_panda, 100);
        ((Grid)Content).Children.Add(_panda);
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

    private async void WelcomeContinue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WelcomeFlag)!);
            await File.WriteAllTextAsync(WelcomeFlag, DateTime.UtcNow.ToString("O"));
        }
        catch { }
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("WelcomeOutro"));
        _panda?.Play(PandaMood.Success);
        await Task.Delay(280);
        WelcomeOverlay.Visibility = Visibility.Collapsed;
        _welcomeVisible = false;
        if (_pendingUpdate != null) ShowPendingUpdate();
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browserReady) return;
        try
        {
            var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            _environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, new CoreWebView2EnvironmentOptions());
            _browserReady = true;
            PrivacyStats.Text = "Orvian schützt deine Sitzung • Browserengine bereit";
            await AddTabAsync(true);
            _panda?.Play(PandaMood.Success);
        }
        catch (Exception ex)
        {
            PrivacyStats.Text = "Start der Browserengine fehlgeschlagen";
            _panda?.Play(PandaMood.Error);
            MessageBox.Show("Orvian konnte die WebView2-Browserengine nicht starten.\n\n" + ex.Message, "Orvian – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task AddTabAsync(bool select)
    {
        if (!_browserReady || _environment == null) return;

        var view = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, DefaultBackgroundColor = System.Drawing.Color.White };
        var header = BuildTabHeader();
        var tab = new BrowserTab { View = view, HeaderButton = header };
        header.Tag = tab;

        HookWebView(view);
        _tabs.Add(tab);
        BrowserHost.Content = view;
        UpdateTabVisuals();

        try
        {
            await view.EnsureCoreWebView2Async(_environment);
            ConfigureCore(view.CoreWebView2);
            if (select) SelectTab(tab);
            tab.InternalPage = true;
            await NavigateInternalAsync(tab, NewTabAddress, "Neuer Tab", NewTabHtml);
        }
        catch (Exception ex)
        {
            if (_tabs.Contains(tab)) _tabs.Remove(tab);
            MessageBox.Show("Der neue Tab konnte nicht gestartet werden.\n\n" + ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Button BuildTabHeader()
    {
        var close = new Button { Content = "×", Width = 27, Height = 27, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Tab schließen" };
        close.Click += CloseTab_Click;
        var title = new TextBlock { Text = "Neuer Tab", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 165, FontWeight = FontWeights.SemiBold };
        var icon = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = (Brush)FindResource("AccentBrush"), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "O", Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.Children.Add(icon);
        Grid.SetColumn(title, 1); layout.Children.Add(title);
        Grid.SetColumn(close, 2); layout.Children.Add(close);
        var button = new Button { Style = (Style)FindResource("TabHeaderButton"), Content = layout };
        button.Click += TabHeader_Click;
        return button;
    }

    private void TabHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is BrowserTab tab) SelectTab(tab);
    }

    private void SelectTab(BrowserTab tab)
    {
        if (!_tabs.Contains(tab)) return;
        _activeTab = tab;
        BrowserHost.Content = tab.View;
        tab.View.Visibility = Visibility.Visible;
        AddressBox.Text = tab.InternalPage ? (AddressBox.Text == NewTabAddress ? NewTabAddress : AddressBox.Text) : tab.View.Source?.ToString() ?? "";
        TabStrip.Children.Clear();
        foreach (var item in _tabs) TabStrip.Children.Add(item.HeaderButton);
        UpdateTabVisuals();
        UpdateNavigationUi();
        tab.View.Focus();
    }

    private void UpdateTabVisuals()
    {
        foreach (var tab in _tabs)
        {
            tab.HeaderButton.Background = tab == _activeTab ? (Brush)FindResource("AccentSoftBrush") : Brushes.White;
            tab.HeaderButton.BorderBrush = tab == _activeTab ? (Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromRgb(212, 221, 232));
        }
    }

    private void UpdateTabTitle(BrowserTab tab, string title)
    {
        tab.Title = string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title;
        if (tab.HeaderButton.Content is Grid grid && grid.Children.OfType<TextBlock>().FirstOrDefault() is { } titleBlock)
            titleBlock.Text = tab.Title;
        if (tab == _activeTab) TabTitleDummy();
    }

    private void TabTitleDummy() { }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button close || close.Parent is not Grid grid || grid.Parent is not Button header || header.Tag is not BrowserTab tab) return;
        e.Handled = true;
        var index = _tabs.IndexOf(tab);
        if (index < 0) return;
        var wasActive = tab == _activeTab;
        _tabs.RemoveAt(index);
        try { tab.View.Dispose(); } catch { }
        if (_tabs.Count == 0)
        {
            _ = AddTabAsync(true);
            return;
        }
        if (wasActive)
        {
            var next = _tabs[Math.Min(index, _tabs.Count - 1)];
            SelectTab(next);
        }
        else
        {
            RebuildTabStrip();
        }
        _panda?.Play(PandaMood.Happy);
    }

    private void RebuildTabStrip()
    {
        TabStrip.Children.Clear();
        foreach (var tab in _tabs) TabStrip.Children.Add(tab.HeaderButton);
        UpdateTabVisuals();
    }

    private void HookWebView(WebView2 view)
    {
        view.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
    }

    private void CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (sender is not WebView2 view || view.CoreWebView2 == null) return;
        if (e.IsSuccess) ConfigureCore(view.CoreWebView2);
    }

    private void ConfigureCore(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = true;
        try { core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All); } catch { }
        core.WebResourceRequested -= WebResourceRequested;
        core.WebResourceRequested += WebResourceRequested;
        core.NavigationStarting -= NavigationStarting;
        core.NavigationStarting += NavigationStarting;
        core.NavigationCompleted -= NavigationCompleted;
        core.NavigationCompleted += NavigationCompleted;
        core.NewWindowRequested -= Core_NewWindowRequested;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.WebMessageReceived -= WebMessageReceived;
        core.WebMessageReceived += WebMessageReceived;
        core.DownloadStarting -= DownloadStarting;
        core.DownloadStarting += DownloadStarting;
        core.PermissionRequested -= PermissionRequested;
        core.PermissionRequested += PermissionRequested;
    }

    private BrowserTab? TabFor(CoreWebView2? core)
        => _tabs.FirstOrDefault(x => ReferenceEquals(x.View.CoreWebView2, core));

    private BrowserTab? TabFor(object? sender)
        => sender is CoreWebView2 core ? TabFor(core) : sender is WebView2 view ? _tabs.FirstOrDefault(x => ReferenceEquals(x.View, view)) : null;

    private async Task WarmupProtectionAsync()
    {
        await _blocker.RefreshFiltersAsync();
        await Dispatcher.InvokeAsync(() => BlockerStats.Text = $"• {_blocker.RuleCount:N0} Schutzregeln aktiv");
    }

    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
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
            if (!_welcomeVisible) ShowPendingUpdate();
        }
        catch { }
    }

    private void ShowPendingUpdate()
    {
        if (_pendingUpdate == null) return;
        UpdateText.Text = $"Orvian {_updateChecker.CurrentVersion} ist installiert. Version {_pendingUpdate.Version} ist verfügbar." + (_pendingUpdate.HasInstaller ? " Der Installer ist direkt bereit." : " Der Installer wird auf der Release-Seite bereitgestellt.");
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
        if (_pendingUpdate is null) return;
        if (!_pendingUpdate.HasInstaller)
        {
            Process.Start(new ProcessStartInfo("https://github.com/paulhubacek1-afk/orvian-browser/releases/latest") { UseShellExecute = true });
            return;
        }
        UpdateNowButton.IsEnabled = false;
        UpdateNowButton.Content = "Wird heruntergeladen …";
        try
        {
            var path = await _updateChecker.DownloadInstallerAsync(_pendingUpdate);
            if (path is null) throw new InvalidOperationException("Installer konnte nicht heruntergeladen werden.");
            _panda?.Play(PandaMood.Success);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateNowButton.IsEnabled = true;
            UpdateNowButton.Content = "Jetzt aktualisieren";
            _panda?.Play(PandaMood.Error);
            MessageBox.Show("Das Update konnte nicht gestartet werden.\n\n" + ex.Message, "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(e.Uri)) return;
        var current = TabFor(sender);
        if (current != null) current.View.CoreWebView2.Navigate(e.Uri);
    }

    private void WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            if (_blocker.ShouldBlock(e.Request.Uri))
                e.Response = e.ResourceContext == CoreWebView2WebResourceContext.Document
                    ? null
                    : TabFor(sender)?.View.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked by Orvian", "Content-Type: text/plain");
        }
        catch { }
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.TryGetWebMessageAsString();
        try
        {
            if (message.StartsWith("search:", StringComparison.Ordinal))
            {
                var query = message[7..].Trim();
                if (!string.IsNullOrWhiteSpace(query)) CurrentView()?.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(query));
                return;
            }
            switch (message)
            {
                case "settings": Menu_Click(this, new RoutedEventArgs()); break;
                case "vault": OpenVault(); break;
                case "history": await OpenHistoryPageAsync(); break;
                case "bookmarks": await OpenBookmarksPageAsync(); break;
                case "downloads": OpenDownloadsPage(); break;
                case "privacy": OpenPrivacyPage(); break;
                case "permissions": await OpenPermissionsPageAsync(); break;
                case "newtab": await AddTabAsync(true); break;
            }
        }
        catch { }
    }

    private WebView2? CurrentView() => _activeTab?.View;

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
            var kind = e.PermissionKind.ToString();
            var origin = new Uri(e.Uri).GetLeftPart(UriPartial.Authority);
            var allow = MessageBox.Show($"{origin}\n\nDie Website möchte Zugriff auf: {kind}.\n\nZugriff erlauben?", "Orvian Berechtigung", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            e.State = allow ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            await _data.AddPermissionAsync(origin, kind, allow ? "Erlaubt" : "Abgelehnt");
            _panda?.Play(allow ? PandaMood.Success : PandaMood.Privacy);
        }
        catch { e.State = CoreWebView2PermissionState.Deny; }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var tab = TabFor(sender);
        if (tab == null) return;
        if (e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) tab.InternalPage = false;
        PrivacyStats.Text = "Orvian lädt die Seite …";
        if (_blocker.IsBlockedHost(e.Uri))
        {
            e.Cancel = true;
            _ = NavigateInternalAsync(tab, "orvian://blocked", "Blockiert", $"<h1>🛡 Seite blockiert</h1><p>Orvian hat <b>{WebUtility.HtmlEncode(e.Uri)}</b> als geschützte Ressource erkannt.</p>");
            _panda?.Play(PandaMood.Error);
            return;
        }
        _panda?.Play(PandaMood.Loading);
    }

    private async void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var tab = TabFor(sender);
        if (tab == null) return;
        UpdateNavigationUi();
        if (e.IsSuccess)
        {
            PrivacyStats.Text = "Orvian schützt deine Sitzung • Seite bereit";
            var title = tab.View.CoreWebView2.DocumentTitle;
            UpdateTabTitle(tab, string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title);
            if (!tab.InternalPage && tab.View.Source != null)
                await _data.AddHistoryAsync(tab.View.Source.ToString(), tab.Title);
            _panda?.Play(PandaMood.Success);
        }
        else
        {
            PrivacyStats.Text = "Orvian • Seite konnte nicht vollständig geladen werden";
            _panda?.Play(PandaMood.Error);
        }
    }

    private void UpdateNavigationUi()
    {
        var tab = _activeTab;
        if (tab == null) return;
        if (tab.InternalPage) { AddressBox.Text = tab.View.Source?.ToString()?.StartsWith("about:", StringComparison.OrdinalIgnoreCase) == true ? AddressBox.Text : AddressBox.Text; }
        else AddressBox.Text = tab.View.Source?.ToString() ?? "";
        BackButton.IsEnabled = tab.View.CanGoBack;
        ForwardButton.IsEnabled = tab.View.CanGoForward;
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !_browserReady) return;
        NavigateFromAddress();
        e.Handled = true;
    }

    private void NavigateFromAddress()
    {
        var value = AddressBox.Text.Trim();
        var view = CurrentView();
        if (string.IsNullOrWhiteSpace(value) || view == null) return;
        switch (value.ToLowerInvariant())
        {
            case "orvian://newtab": _ = NavigateInternalAsync(_activeTab!, NewTabAddress, "Neuer Tab", NewTabHtml); return;
            case "orvian://history": _ = OpenHistoryPageAsync(); return;
            case "orvian://bookmarks": _ = OpenBookmarksPageAsync(); return;
            case "orvian://downloads": OpenDownloadsPage(); return;
            case "orvian://privacy": OpenPrivacyPage(); return;
            case "orvian://permissions": _ = OpenPermissionsPageAsync(); return;
            case "orvian://vault": OpenVault(); return;
        }
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && (absolute.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || absolute.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))) { view.CoreWebView2.Navigate(absolute.ToString()); return; }
        if (Uri.TryCreate("http://" + value, UriKind.Absolute, out var local) && (local.Host == "localhost" || local.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))) { view.CoreWebView2.Navigate(local.ToString()); return; }
        view.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    private async Task NavigateInternalAsync(BrowserTab tab, string address, string title, string html)
    {
        if (tab.View.CoreWebView2 == null) return;
        tab.InternalPage = true;
        tab.Title = title;
        if (tab == _activeTab) AddressBox.Text = address;
        UpdateTabTitle(tab, title);
        tab.View.CoreWebView2.NavigateToString(html);
        await Task.CompletedTask;
    }

    private Task OpenNewTabPage()
    {
        if (_activeTab == null) return Task.CompletedTask;
        return NavigateInternalAsync(_activeTab, NewTabAddress, "Neuer Tab", NewTabHtml);
    }

    private string HtmlPage(string title, string body) => $$"""
        <!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{{WebUtility.HtmlEncode(title)}}</title><style>*{box-sizing:border-box}body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:linear-gradient(135deg,#f7f9fc,#edf5ff);color:#182235;padding:42px}.wrap{max-width:1080px;margin:auto}h1{font-size:42px;margin:8px 0}p{color:#68758b;font-size:16px}.list{display:grid;gap:10px;margin-top:22px}.row{display:block;padding:16px 18px;background:#fff;border:1px solid #d8e2ed;border-radius:16px;text-decoration:none;color:#182235}.row span{display:block;color:#6d7a8e;font-size:13px;margin-top:5px}.empty{padding:22px;background:#fff;border:1px dashed #cbd6e3;border-radius:16px;color:#748095}.stats{display:flex;gap:14px;margin-top:22px}.stats>div{flex:1;background:#fff;border:1px solid #d8e2ed;border-radius:18px;padding:20px}.stats b,.stats span{display:block}.stats b{font-size:25px;color:#2f6bff}.stats span{margin-top:5px;color:#6d7a8e}</style></head><body><div class='wrap'>{{body}}</div></body></html>
        """;

    private async Task OpenHistoryPageAsync()
    {
        var items = await _data.GetHistoryAsync();
        var rows = new StringBuilder();
        foreach (var item in items.Take(80)) rows.Append($"<a class='row' href='{WebUtility.HtmlEncode(item.Url)}'><b>{WebUtility.HtmlEncode(item.Title)}</b><span>{WebUtility.HtmlEncode(item.Url)} • {item.VisitedAt:dd.MM.yyyy HH:mm}</span></a>");
        await NavigateInternalAsync(_activeTab!, "orvian://history", "Verlauf", HtmlPage("Verlauf", $"<h1>🕘 Verlauf</h1><p>Deine letzten Seiten – lokal in Orvian gespeichert.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch kein Verlauf vorhanden.</div>" : rows.ToString())}</div>"));
    }

    private async Task OpenBookmarksPageAsync()
    {
        var items = await _data.GetBookmarksAsync();
        var rows = new StringBuilder();
        foreach (var item in items) rows.Append($"<a class='row' href='{WebUtility.HtmlEncode(item.Url)}'><b>★ {WebUtility.HtmlEncode(item.Title)}</b><span>{WebUtility.HtmlEncode(item.Url)}</span></a>");
        await NavigateInternalAsync(_activeTab!, "orvian://bookmarks", "Lesezeichen", HtmlPage("Lesezeichen", $"<h1>★ Lesezeichen</h1><p>Deine gespeicherten Seiten.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Lesezeichen.</div>" : rows.ToString())}</div>"));
    }

    private void OpenDownloadsPage()
    {
        var rows = new StringBuilder();
        foreach (var item in _downloads) rows.Append($"<div class='row'><b>📥 {WebUtility.HtmlEncode(item.FileName)}</b><span>{WebUtility.HtmlEncode(item.Path)} • gestartet {item.StartedAt:HH:mm}</span></div>");
        _ = NavigateInternalAsync(_activeTab!, "orvian://downloads", "Downloads", HtmlPage("Downloads", $"<h1>📥 Downloads</h1><p>Download-Historie dieser Sitzung.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Downloads.</div>" : rows.ToString())}</div>"));
    }

    private void OpenPrivacyPage()
    {
        var blocked = Math.Max(0, _blocker.RuleCount);
        _ = NavigateInternalAsync(_activeTab!, "orvian://privacy", "Datenschutz", HtmlPage("Datenschutz", $"<h1>🛡 Datenschutz-Center</h1><p>Orvian schützt Netzwerkressourcen mit lokalem Schutz und aktualisierten Filterregeln.</p><div class='stats'><div><b>{blocked:N0}</b><span>Filterregeln</span></div><div><b>Aktiv</b><span>Tracker-Schutz</span></div><div><b>Lokal</b><span>Datenhaltung</span></div></div>"));
    }

    private async Task OpenPermissionsPageAsync()
    {
        var items = await _data.GetPermissionsAsync();
        var rows = new StringBuilder();
        foreach (var item in items) rows.Append($"<div class='row'><b>{WebUtility.HtmlEncode(item.Origin)}</b><span>{WebUtility.HtmlEncode(item.Permission)} • {WebUtility.HtmlEncode(item.Decision)}</span></div>");
        await NavigateInternalAsync(_activeTab!, "orvian://permissions", "Berechtigungen", HtmlPage("Berechtigungen", $"<h1>🔐 Berechtigungen</h1><p>Entscheidungen werden lokal gespeichert.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Berechtigungen gespeichert.</div>" : rows.ToString())}</div>"));
    }

    private void OpenVault()
    {
        new PasswordVaultWindow(_vault) { Owner = this }.ShowDialog();
    }

    private void NavigateToCommand(string id)
    {
        switch (id)
        {
            case "newtab": _ = AddTabAsync(true); break;
            case "history": _ = OpenHistoryPageAsync(); break;
            case "bookmarks": _ = OpenBookmarksPageAsync(); break;
            case "downloads": OpenDownloadsPage(); break;
            case "privacy": OpenPrivacyPage(); break;
            case "permissions": _ = OpenPermissionsPageAsync(); break;
            case "settings": Menu_Click(this, new RoutedEventArgs()); break;
            case "vault": OpenVault(); break;
            case "update": _ = ForceUpdateCheckAsync(); break;
            case "reload": CurrentView()?.Reload(); break;
            case "focus": AddressBox.Focus(); AddressBox.SelectAll(); break;
        }
    }

    private async Task ForceUpdateCheckAsync()
    {
        _panda?.Play(PandaMood.Thinking);
        try
        {
            var update = await _updateChecker.GetLatestAsync();
            if (update == null || update.Version <= _updateChecker.CurrentVersion)
            {
                _panda?.Play(PandaMood.Success);
                MessageBox.Show($"Du verwendest bereits Orvian {_updateChecker.CurrentVersion}.", "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _pendingUpdate = update;
            ShowPendingUpdate();
        }
        catch (Exception ex)
        {
            _panda?.Play(PandaMood.Error);
            MessageBox.Show("Die Update-Prüfung ist momentan nicht erreichbar.\n\n" + ex.Message, "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (CurrentView()?.CanGoBack == true) CurrentView()!.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (CurrentView()?.CanGoForward == true) CurrentView()!.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => CurrentView()?.Reload();
    private void Home_Click(object sender, RoutedEventArgs e) => _ = OpenNewTabPage();
    private void NewTab_Click(object sender, RoutedEventArgs e) => _ = AddTabAsync(true);

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        var view = CurrentView();
        if (view?.Source == null || _activeTab?.InternalPage == true) return;
        await _data.ToggleBookmarkAsync(view.Source.ToString(), view.CoreWebView2.DocumentTitle);
        _panda?.Play(PandaMood.Success);
    }

    private void Privacy_Click(object sender, RoutedEventArgs e) { OpenPrivacyPage(); _panda?.Play(PandaMood.Privacy); }
    private void Menu_Click(object sender, RoutedEventArgs e) => new SettingsWindow { Owner = this }.ShowDialog();
    private void InstallApp_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Diese Funktion ist für eine spätere Stable-Ausbaustufe reserviert.", "Orvian");

    private async void CheckPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordPrompt { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var result = await PasswordSecurity.CheckPwnedAsync(dialog.Password);
        dialog.Close();
        MessageBox.Show(result, "Orvian Passwortschutz");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (e.Key == Key.C) { LockBrowser(); e.Handled = true; return; }
            if (e.Key == Key.D) { UnlockBrowser(); e.Handled = true; return; }
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.K: ShowCommandPalette(); e.Handled = true; return;
                case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; return;
                case Key.T: _ = AddTabAsync(true); e.Handled = true; return;
                case Key.W: CloseActiveTab(); e.Handled = true; return;
                case Key.H: _ = OpenHistoryPageAsync(); e.Handled = true; return;
                case Key.J: OpenDownloadsPage(); e.Handled = true; return;
                case Key.R: CurrentView()?.Reload(); e.Handled = true; return;
                case Key.D: _ = Bookmark_ClickAsync(); e.Handled = true; return;
            }
        }
    }

    private void CloseActiveTab()
    {
        if (_activeTab?.HeaderButton is { } header && header.Content is Grid grid && grid.Children.OfType<Button>().FirstOrDefault() is { } close)
            CloseTab_Click(close, new RoutedEventArgs());
    }

    private async Task Bookmark_ClickAsync()
    {
        var view = CurrentView();
        if (view?.Source == null || _activeTab?.InternalPage == true) return;
        await _data.ToggleBookmarkAsync(view.Source.ToString(), view.CoreWebView2.DocumentTitle);
        _panda?.Play(PandaMood.Success);
    }

    private void ShowCommandPalette()
    {
        var dialog = new CommandPaletteWindow(Commands) { Owner = this };
        dialog.CommandInvoked += (_, id) => Dispatcher.BeginInvoke(() => NavigateToCommand(id));
        dialog.ShowDialog();
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
