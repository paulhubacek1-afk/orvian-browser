using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow : Window
{
    private readonly Blocker _blocker = new();
    private readonly UpdateChecker _updateChecker = new();
    private readonly BrowserDataStore _data = new();
    private readonly List<(string FileName, string Path, DateTimeOffset StartedAt)> _downloads = new();
    private PandaControl? _panda;
    private bool _locked;
    private bool _browserReady;
    private bool _welcomeVisible;
    private bool _internalPage;
    private UpdateInfo? _pendingUpdate;
    private DispatcherTimer? _updateTimer;
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
        new BrowserCommand("update", "Nach Updates suchen", "Orvian auf neue Version prüfen", ""),
        new BrowserCommand("reload", "Seite neu laden", "Aktuelle Seite neu laden", "Ctrl+R"),
        new BrowserCommand("focus", "Adressleiste", "URL oder Suche eingeben", "Ctrl+L")
    };

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        BeginIntro();
        ShowWelcomeIfNeeded();
        StartUpdateTimer();
        AddAnimatedPanda();
        _ = InitializeBrowserAsync();
        _ = WarmupProtectionAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e) => _updateTimer?.Stop();
    private void BeginIntro() => BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("Intro"));

    private void AddAnimatedPanda()
    {
        if (_panda != null || Content is not Grid root) return;
        _panda = new PandaControl
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 24, 22)
        };
        Grid.SetRow(_panda, 3);
        Panel.SetZIndex(_panda, 40);
        root.Children.Add(_panda);
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
        OpenNewTabPage();
        _ = CheckForUpdatesAsync();
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browserReady) return;
        try
        {
            var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var options = new CoreWebView2EnvironmentOptions();
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await BrowserView.EnsureCoreWebView2Async(environment);

            var core = BrowserView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
            core.WebResourceRequested += WebResourceRequested;
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.WebMessageReceived += WebMessageReceived;
            core.DownloadStarting += DownloadStarting;
            core.PermissionRequested += PermissionRequested;

            _browserReady = true;
            PrivacyStats.Text = "Orvian schützt deine Sitzung • WebView2 bereit";
            OpenNewTabPage();
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            PrivacyStats.Text = "Start der Browser-Engine fehlgeschlagen";
            _panda?.Play(PandaMood.Error);
            MessageBox.Show("Orvian konnte die WebView2-Browserengine nicht starten.\n\n" + ex.Message + "\n\nInstalliere die aktuelle Microsoft Edge WebView2 Runtime und starte Orvian erneut.", "Orvian – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task WarmupProtectionAsync()
    {
        await _blocker.RefreshFiltersAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            BlockerStats.Text = $"• {Math.Max(0, _blocker.RuleCount):N0} Schutzregeln aktiv";
            if (_browserReady) PrivacyStats.Text = "Orvian schützt deine Sitzung • Schutzfilter aktuell";
        });
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
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (_welcomeVisible) return;
            var update = await _updateChecker.GetLatestAsync();
            if (update is null || update.Version <= _updateChecker.CurrentVersion) return;
            _pendingUpdate = update;
            UpdateText.Text = $"Orvian {_updateChecker.CurrentVersion} ist installiert. Version {update.Version} ist auf GitHub verfügbar.";
            UpdateOverlay.Visibility = Visibility.Visible;
            _panda?.Play(PandaMood.Update);
            BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("UpdateIntro"));
        }
        catch { }
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
        if (sender is Button button) { button.IsEnabled = false; button.Content = "Wird heruntergeladen …"; }
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
            _panda?.Play(PandaMood.Error);
            if (sender is Button failedButton) { failedButton.IsEnabled = true; failedButton.Content = "Jetzt aktualisieren"; }
            MessageBox.Show("Das Update konnte nicht gestartet werden.\n\n" + ex.Message, "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (_browserReady && !string.IsNullOrWhiteSpace(e.Uri)) BrowserView.CoreWebView2.Navigate(e.Uri);
    }

    private void WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            if (_blocker.ShouldBlock(e.Request.Uri))
                e.Response = BrowserView.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked by Orvian", "Content-Type: text/plain");
        }
        catch { }
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.TryGetWebMessageAsString();
            if (message.StartsWith("search:", StringComparison.Ordinal))
            {
                var query = message[7..];
                if (!string.IsNullOrWhiteSpace(query)) BrowserView.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(query));
                return;
            }
            switch (message)
            {
                case "settings": Menu_Click(this, new RoutedEventArgs()); break;
                case "history": await OpenHistoryPageAsync(); break;
                case "bookmarks": await OpenBookmarksPageAsync(); break;
                case "downloads": OpenDownloadsPage(); break;
                case "privacy": OpenPrivacyPage(); break;
                case "permissions": await OpenPermissionsPageAsync(); break;
                case "newtab": OpenNewTabPage(); break;
            }
        }
        catch { }
    }

    private void DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            var downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloadDir);
            var suggested = Path.GetFileName(e.ResultFilePath);
            if (string.IsNullOrWhiteSpace(suggested)) suggested = "Orvian-Download";
            var target = Path.Combine(downloadDir, suggested);
            e.ResultFilePath = target;
            _downloads.Insert(0, (suggested, target, DateTimeOffset.Now));
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
            var decision = MessageBox.Show($"{origin}\n\nDie Website möchte Zugriff auf: {kind}.\n\nZugriff erlauben?", "Orvian Berechtigung", MessageBoxButton.YesNo, MessageBoxImage.Question);
            var allow = decision == MessageBoxResult.Yes;
            e.State = allow ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            await _data.AddPermissionAsync(origin, kind, allow ? "Erlaubt" : "Abgelehnt");
            _panda?.Play(allow ? PandaMood.Success : PandaMood.Privacy);
        }
        catch { e.State = CoreWebView2PermissionState.Deny; }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && !e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) _internalPage = false;
        PrivacyStats.Text = "Orvian lädt die Seite …";
        if (_blocker.IsBlockedHost(e.Uri))
        {
            e.Cancel = true;
            _internalPage = true;
            OpenBlockedPage(e.Uri);
            _panda?.Play(PandaMood.Error);
        }
        else _panda?.Play(PandaMood.Loading);
    }

    private async void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var title = BrowserView.CoreWebView2.DocumentTitle;
        if (!_internalPage && BrowserView.Source != null) AddressBox.Text = BrowserView.Source.ToString();
        BackButton.IsEnabled = BrowserView.CanGoBack;
        ForwardButton.IsEnabled = BrowserView.CanGoForward;
        PrivacyStats.Text = e.IsSuccess ? "Orvian schützt deine Sitzung • Seite bereit" : "Orvian • Seite konnte nicht vollständig geladen werden";
        TabTitle.Text = string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title;
        if (e.IsSuccess) _panda?.Play(PandaMood.Success); else _panda?.Play(PandaMood.Error);
        if (!_internalPage && BrowserView.Source != null) await _data.AddHistoryAsync(BrowserView.Source.ToString(), TabTitle.Text);
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !_browserReady) return;
        NavigateFromAddress(); e.Handled = true;
    }

    private void NavigateFromAddress()
    {
        var value = AddressBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.StartsWith("orvian://", StringComparison.OrdinalIgnoreCase))
        {
            switch (value.ToLowerInvariant())
            {
                case "orvian://newtab": OpenNewTabPage(); return;
                case "orvian://history": _ = OpenHistoryPageAsync(); return;
                case "orvian://bookmarks": _ = OpenBookmarksPageAsync(); return;
                case "orvian://downloads": OpenDownloadsPage(); return;
                case "orvian://privacy": OpenPrivacyPage(); return;
                case "orvian://permissions": _ = OpenPermissionsPageAsync(); return;
            }
        }
        if (TryBuildNetworkUrl(value, out var url)) { BrowserView.CoreWebView2.Navigate(url); return; }
        BrowserView.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    private static bool TryBuildNetworkUrl(string value, out string url)
    {
        url = string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && (absolute.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || absolute.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))) { url = absolute.ToString(); return true; }
        if (IPAddress.TryParse(value, out var directIp)) { url = directIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"http://[{value}]/" : $"http://{value}/"; return true; }
        if (Uri.TryCreate("http://" + value, UriKind.Absolute, out var localOrIp) && (localOrIp.HostNameType == UriHostNameType.IPv4 || localOrIp.HostNameType == UriHostNameType.IPv6)) { url = localOrIp.ToString(); return true; }
        if (value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) { url = "http://" + value; return true; }
        if (Uri.TryCreate("https://" + value, UriKind.Absolute, out var domain) && !string.IsNullOrWhiteSpace(domain.Host)) { url = domain.ToString(); return true; }
        return false;
    }

    private void OpenNewTabPage()
    {
        if (!_browserReady) return;
        _internalPage = true;
        AddressBox.Text = NewTabAddress;
        TabTitle.Text = "Neuer Tab";
        BrowserView.CoreWebView2.NavigateToString(BuildNewTabHtml());
        _panda?.Play(PandaMood.Happy);
    }

    private string BuildNewTabHtml() => HtmlPage("Neuer Tab", @"
<div class='hero'><div class='panda'>🐼</div><div><div class='eyebrow'>ORVIAN BROWSER</div><h1>Dein Browser. Dein Raum.</h1><p>Privat, schnell und mit einem kleinen Panda als Begleitung.</p></div></div>
<form id='search' class='search'><input id='q' autocomplete='off' placeholder='Suchen oder Adresse eingeben …'/><button>⌕</button></form>
<div class='grid'>
<button class='card' onclick="go('https://www.google.com/')"><b>🔎 Google</b><span>Web durchsuchen</span></button>
<button class='card' onclick="go('https://github.com/')"><b>◆ GitHub</b><span>Code & Projekte</span></button>
<button class='card' onclick="go('https://www.youtube.com/')"><b>▶ YouTube</b><span>Videos</span></button>
<button class='card' onclick="msg('privacy')"><b>🛡 Datenschutz</b><span>Schutzstatus ansehen</span></button>
<button class='card' onclick="msg('history')"><b>🕘 Verlauf</b><span>Zuletzt besuchte Seiten</span></button>
<button class='card' onclick="msg('bookmarks')"><b>★ Lesezeichen</b><span>Gespeicherte Seiten</span></button>
</div><div class='tip'>Tipp: <b>Ctrl + K</b> öffnet die Command Palette.</div>
<script>const w=window.chrome?.webview;function msg(x){w?.postMessage(x)}function go(x){location.href=x}document.getElementById('search').onsubmit=(e)=>{e.preventDefault();msg('search:'+document.getElementById('q').value)};</script>");

    private string HtmlPage(string title, string body) => $@"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{WebUtility.HtmlEncode(title)}</title><style>
*{{box-sizing:border-box}}body{{margin:0;font-family:Segoe UI,Arial,sans-serif;background:linear-gradient(135deg,#f7f9fc,#edf5ff);color:#182235;padding:42px}}.wrap{{max-width:1080px;margin:auto}}.hero{{display:flex;align-items:center;gap:26px;margin:28px 0 32px}}.panda{{width:112px;height:112px;border-radius:34px;display:grid;place-items:center;background:white;box-shadow:0 16px 44px #2d6bff18;font-size:64px}}.eyebrow{{letter-spacing:2px;font-weight:800;color:#2f6bff;font-size:12px}}h1{{font-size:42px;margin:4px 0 6px}}p{{color:#68758b;font-size:16px}}.search{{display:flex;gap:10px;background:white;border:1px solid #d7e1ed;border-radius:20px;padding:9px;box-shadow:0 12px 32px #26364c12}}.search input{{flex:1;border:0;outline:0;padding:12px 16px;font-size:18px}}.search button{{border:0;border-radius:14px;background:#2f6bff;color:white;width:56px;font-size:22px}}.grid{{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-top:24px}}.card{{border:1px solid #d8e2ed;background:white;border-radius:18px;padding:18px;text-align:left;cursor:pointer;box-shadow:0 8px 24px #26364c0d}}.card:hover{{transform:translateY(-2px);box-shadow:0 14px 28px #26364c18}}.card b,.card span{{display:block}}.card span{{color:#6f7d91;margin-top:6px}}.tip{{margin-top:28px;color:#6e7a8c;font-size:13px}}</style></head><body><div class='wrap'>{body}</div></body></html>";

    private async Task OpenHistoryPageAsync()
    {
        var items = await _data.GetHistoryAsync();
        var rows = new StringBuilder();
        foreach (var item in items.Take(80)) rows.Append($"<a class='row' href='{WebUtility.HtmlEncode(item.Url)}'><b>{WebUtility.HtmlEncode(item.Title)}</b><span>{WebUtility.HtmlEncode(item.Url)} • {item.VisitedAt:dd.MM.yyyy HH:mm}</span></a>");
        OpenInternal(HtmlPage("Verlauf", $"<h1>🕘 Verlauf</h1><p>Deine letzten Seiten – lokal in Orvian gespeichert.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch kein Verlauf vorhanden.</div>" : rows.ToString())}</div>"), "orvian://history", "Verlauf");
    }

    private async Task OpenBookmarksPageAsync()
    {
        var items = await _data.GetBookmarksAsync();
        var rows = new StringBuilder();
        foreach (var item in items) rows.Append($"<a class='row' href='{WebUtility.HtmlEncode(item.Url)}'><b>★ {WebUtility.HtmlEncode(item.Title)}</b><span>{WebUtility.HtmlEncode(item.Url)}</span></a>");
        OpenInternal(HtmlPage("Lesezeichen", $"<h1>★ Lesezeichen</h1><p>Deine gespeicherten Seiten.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Lesezeichen. Klicke auf ☆ neben der Adressleiste.</div>" : rows.ToString())}</div>"), "orvian://bookmarks", "Lesezeichen");
    }

    private void OpenDownloadsPage()
    {
        var rows = new StringBuilder();
        foreach (var item in _downloads) rows.Append($"<div class='row'><b>📥 {WebUtility.HtmlEncode(item.FileName)}</b><span>{WebUtility.HtmlEncode(item.Path)} • gestartet {item.StartedAt:HH:mm}</span></div>");
        OpenInternal(HtmlPage("Downloads", $"<h1>📥 Downloads</h1><p>Aktuelle Download-Historie dieser Sitzung.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Downloads.</div>" : rows.ToString())}</div>"), "orvian://downloads", "Downloads");
    }

    private void OpenPrivacyPage()
    {
        var blocked = Math.Max(0, _blocker.RuleCount);
        OpenInternal(HtmlPage("Datenschutz", $"<h1>🛡 Datenschutz-Center</h1><p>Orvian schützt Netzwerkressourcen mit lokalen und aktualisierten Filterregeln.</p><div class='stats'><div><b>{blocked:N0}</b><span>Filterregeln</span></div><div><b>Aktiv</b><span>Tracker-Schutz</span></div><div><b>Lokal</b><span>Datenhaltung</span></div></div><p style='margin-top:24px'>Per-Website-Berechtigungen werden beim ersten Zugriff abgefragt und lokal protokolliert.</p>"), "orvian://privacy", "Datenschutz");
    }

    private async Task OpenPermissionsPageAsync()
    {
        var items = await _data.GetPermissionsAsync();
        var rows = new StringBuilder();
        foreach (var item in items) rows.Append($"<div class='row'><b>{WebUtility.HtmlEncode(item.Origin)}</b><span>{WebUtility.HtmlEncode(item.Permission)} • {WebUtility.HtmlEncode(item.Decision)}</span></div>");
        OpenInternal(HtmlPage("Berechtigungen", $"<h1>🔐 Berechtigungen</h1><p>Entscheidungen werden lokal gespeichert.</p><div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Berechtigungen gespeichert.</div>" : rows.ToString())}</div>"), "orvian://permissions", "Berechtigungen");
    }

    private void OpenBlockedPage(string url) => OpenInternal(HtmlPage("Blockiert", $"<h1>🛡 Seite blockiert</h1><p>Orvian hat <b>{WebUtility.HtmlEncode(url)}</b> als geschützte Ressource erkannt.</p><p>Du kannst die Adresse ändern oder zurück navigieren.</p>"), "orvian://blocked", "Blockiert");

    private void OpenInternal(string html, string address, string title)
    {
        if (!_browserReady) return;
        _internalPage = true;
        AddressBox.Text = address;
        TabTitle.Text = title;
        var css = "<style>.list{display:grid;gap:10px;margin-top:22px}.row{display:block;padding:16px 18px;background:#fff;border:1px solid #d8e2ed;border-radius:16px;text-decoration:none;color:#182235}.row span{display:block;color:#6d7a8e;font-size:13px;margin-top:5px}.empty{padding:22px;background:#fff;border:1px dashed #cbd6e3;border-radius:16px;color:#748095}.stats{display:flex;gap:14px;margin-top:22px}.stats>div{flex:1;background:#fff;border:1px solid #d8e2ed;border-radius:18px;padding:20px}.stats b,.stats span{display:block}.stats b{font-size:25px;color:#2f6bff}.stats span{margin-top:5px;color:#6d7a8e}</style>";
        BrowserView.CoreWebView2.NavigateToString(html.Replace("</body>", css + "</body>"));
    }

    private void NavigateToCommand(string id)
    {
        switch (id)
        {
            case "newtab": OpenNewTabPage(); break;
            case "history": _ = OpenHistoryPageAsync(); break;
            case "bookmarks": _ = OpenBookmarksPageAsync(); break;
            case "downloads": OpenDownloadsPage(); break;
            case "privacy": OpenPrivacyPage(); break;
            case "permissions": _ = OpenPermissionsPageAsync(); break;
            case "settings": Menu_Click(this, new RoutedEventArgs()); break;
            case "update": _ = ForceUpdateCheckAsync(); break;
            case "reload": if (_browserReady) BrowserView.Reload(); break;
            case "focus": AddressBox.Focus(); AddressBox.SelectAll(); break;
        }
    }

    private async Task ForceUpdateCheckAsync()
    {
        _panda?.Play(PandaMood.Thinking);
        var update = await _updateChecker.GetLatestAsync();
        if (update is null || update.Version <= _updateChecker.CurrentVersion)
        {
            _panda?.Play(PandaMood.Success);
            MessageBox.Show($"Du verwendest bereits Orvian {_updateChecker.CurrentVersion}.", "Orvian Update", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _pendingUpdate = update;
        UpdateText.Text = $"Orvian {_updateChecker.CurrentVersion} ist installiert. Version {update.Version} ist auf GitHub verfügbar.";
        UpdateOverlay.Visibility = Visibility.Visible;
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("UpdateIntro"));
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (_browserReady && BrowserView.CanGoBack) BrowserView.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (_browserReady && BrowserView.CanGoForward) BrowserView.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) { if (_browserReady) BrowserView.Reload(); }
    private void Home_Click(object sender, RoutedEventArgs e) => OpenNewTabPage();
    private void NewTab_Click(object sender, RoutedEventArgs e) => OpenNewTabPage();
    private void CloseTab_Click(object sender, RoutedEventArgs e) => OpenNewTabPage();
    private void Tab_Click(object sender, MouseButtonEventArgs e) { if (_browserReady) BrowserView.Focus(); }

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (!_browserReady || BrowserView.Source == null || _internalPage) return;
        var url = BrowserView.Source.ToString();
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        await _data.ToggleBookmarkAsync(url, BrowserView.CoreWebView2.DocumentTitle);
        _panda?.Play(PandaMood.Success);
    }

    private void Privacy_Click(object sender, RoutedEventArgs e) { OpenPrivacyPage(); _panda?.Play(PandaMood.Privacy); }
    private void Menu_Click(object sender, RoutedEventArgs e) => new SettingsWindow { Owner = this }.ShowDialog();
    private void InstallApp_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Orvian erkennt Web-App-Manifeste und kann Websites später als eigene App installieren.", "Website als App");

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
                case Key.T: OpenNewTabPage(); e.Handled = true; return;
                case Key.H: _ = OpenHistoryPageAsync(); e.Handled = true; return;
                case Key.J: OpenDownloadsPage(); e.Handled = true; return;
                case Key.R: if (_browserReady) BrowserView.Reload(); e.Handled = true; return;
                case Key.D: _ = Bookmark_ClickAsync(); e.Handled = true; return;
            }
        }
    }

    private async Task Bookmark_ClickAsync()
    {
        if (!_browserReady || BrowserView.Source == null || _internalPage) return;
        var url = BrowserView.Source.ToString();
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        await _data.ToggleBookmarkAsync(url, BrowserView.CoreWebView2.DocumentTitle);
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
        BrowserView.Visibility = Visibility.Hidden;
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
        BrowserView.Visibility = Visibility.Visible;
        _panda?.Play(PandaMood.Success);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => UnlockBrowser();
}
