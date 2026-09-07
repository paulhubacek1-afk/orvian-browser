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
    private bool _locked;
    private bool _loaded;

    private const string NewTabAddress = "orvian://newtab";
    private static readonly string WelcomeFlag = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orvian",
        "welcome-shown.flag");

    private IReadOnlyList<BrowserCommand> Commands => new[]
    {
        new BrowserCommand("newtab", "Neuer Tab", "Neue Orvian-Startseite öffnen", "Ctrl+T"),
        new BrowserCommand("history", "Verlauf", "Zuletzt besuchte Seiten anzeigen", "Ctrl+H"),
        new BrowserCommand("bookmarks", "Lesezeichen", "Gespeicherte Seiten öffnen", "Ctrl+Shift+O"),
        new BrowserCommand("downloads", "Downloads", "Download-Verlauf anzeigen", "Ctrl+J"),
        new BrowserCommand("privacy", "Datenschutz-Center", "Schutzstatus und Blocker anzeigen", "Ctrl+Shift+P"),
        new BrowserCommand("permissions", "Berechtigungen", "Kamera, Mikrofon und Standort verwalten", ""),
        new BrowserCommand("settings", "Einstellungen", "Orvian konfigurieren", ""),
        new BrowserCommand("vault", "Passwort-Tresor", "Gespeicherte Passwörter verwalten", ""),
        new BrowserCommand("update", "Nach Updates suchen", "Remote nach einer neuen Orvian-Version suchen", ""),
        new BrowserCommand("reload", "Seite neu laden", "Aktuelle Seite neu laden", "Ctrl+R"),
        new BrowserCommand("focus", "Adressleiste", "URL, Datei oder Suchbegriff eingeben", "Ctrl+L")
    };

    private const string NewTabHtml = """
        <!doctype html>
        <html lang="de">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Neuer Tab – Orvian</title>
          <style>
            :root{font-family:Segoe UI,Arial,sans-serif;color:#172236}
            *{box-sizing:border-box}
            body{margin:0;min-height:100vh;background:
              radial-gradient(circle at 78% 8%,rgba(100,165,255,.24),transparent 30%),
              radial-gradient(circle at 15% 82%,rgba(115,205,255,.13),transparent 34%),
              linear-gradient(145deg,#fbfdff 0,#eef5ff 100%)}
            main{max-width:1180px;margin:auto;padding:72px 54px 64px}
            .brand{font-size:12px;letter-spacing:.22em;font-weight:800;color:#2f6bff}
            h1{font-size:58px;letter-spacing:-.04em;line-height:1;margin:10px 0 12px}
            .lead{font-size:19px;color:#66758c;max-width:720px;margin:0 0 34px}
            .search{display:flex;max-width:900px;padding:8px;background:rgba(255,255,255,.92);border:1px solid #cbd8e8;border-radius:25px;box-shadow:0 20px 55px rgba(42,70,105,.13)}
            input{flex:1;border:0;outline:0;background:transparent;font-size:17px;padding:15px 17px;color:#172236}
            .go{width:58px;border:0;border-radius:18px;background:#2f6bff;color:white;font-size:24px;cursor:pointer}
            .section{margin-top:30px}
            .section-title{font-size:13px;text-transform:uppercase;letter-spacing:.16em;color:#718096;font-weight:800;margin-bottom:12px}
            .grid{display:grid;grid-template-columns:repeat(3,1fr);gap:15px;max-width:900px}
            .card{border:1px solid #d5e0ec;background:rgba(255,255,255,.82);border-radius:20px;padding:22px;text-align:left;cursor:pointer;box-shadow:0 10px 30px rgba(45,69,102,.07);transition:.18s}
            .card:hover{transform:translateY(-4px);box-shadow:0 18px 42px rgba(45,69,102,.13);border-color:#b8cdf0}
            .card b,.card span{display:block}.card b{font-size:16px}.card span{margin-top:7px;color:#75839a;font-size:13px}
            .hint{max-width:900px;margin-top:20px;padding:13px 16px;border:1px solid #d8e3ef;border-radius:15px;background:rgba(255,255,255,.55);color:#75839a;font-size:13px}
            @media(max-width:850px){main{padding:42px 24px}h1{font-size:42px}.grid{grid-template-columns:1fr 1fr}}
            @media(max-width:560px){h1{font-size:34px}.grid{grid-template-columns:1fr}}
          </style>
        </head>
        <body>
          <main>
            <div class="brand">ORVIAN BROWSER</div>
            <h1>Dein Browser. Dein Raum.</h1>
            <p class="lead">Schnell, privat und auf Webkompatibilität ausgelegt – mit WebView2, echten Tabs und dem Orvian-Panda als dezenter Browserbegleitung.</p>
            <form id="search" class="search">
              <input id="q" autocomplete="off" autofocus placeholder="Suchen, Website oder HTML-Datei öffnen …">
              <button class="go" aria-label="Suchen">⌕</button>
            </form>

            <div class="section">
              <div class="section-title">Schnellzugriff</div>
              <div class="grid">
                <button class="card" onclick="go('https://www.google.com/')"><b>Google</b><span>Web durchsuchen</span></button>
                <button class="card" onclick="go('https://github.com/')"><b>GitHub</b><span>Code und Projekte</span></button>
                <button class="card" onclick="go('https://www.youtube.com/')"><b>YouTube</b><span>Videos</span></button>
                <button class="card" onclick="msg('privacy')"><b>Datenschutz</b><span>Schutzstatus ansehen</span></button>
                <button class="card" onclick="msg('vault')"><b>Passwort-Tresor</b><span>Passwörter sicher verwalten</span></button>
                <button class="card" onclick="msg('settings')"><b>Einstellungen</b><span>Orvian personalisieren</span></button>
              </div>
            </div>

            <div class="hint">HTML bleibt erlaubt: Öffne lokale <b>.html</b>- und <b>.htm</b>-Dateien direkt über die Adressleiste oder über einen Dateipfad. <b>Ctrl+T</b> öffnet dabei einen echten neuen Tab.</div>
          </main>
          <script>
            const w=window.chrome?.webview;
            function msg(x){w?.postMessage(x)}
            function go(x){location.href=x}
            document.getElementById('search').addEventListener('submit',e=>{
              e.preventDefault();
              const q=document.getElementById('q').value.trim();
              if(q) msg('search:'+q);
            });
          </script>
        </body>
        </html>
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

        _ = InitializeBrowserAsync();
        _ = WarmupProtectionAsync();
        _ = CheckForUpdatesAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _updateTimer?.Stop();
        _panda?.StopAnimations();

        foreach (var tab in _tabs)
        {
            try { tab.View.Dispose(); } catch { }
        }

        _environment = null;
    }

    private void AddAnimatedPanda()
    {
        if (_panda != null) return;

        _panda = new PandaControl
        {
            Width = 112,
            Height = 112,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 18, 18),
            Opacity = 0.94
        };

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

        await Task.Delay(240);
        WelcomeOverlay.Visibility = Visibility.Collapsed;
        _welcomeVisible = false;

        if (_pendingUpdate != null)
            ShowPendingUpdate();
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browserReady) return;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Orvian",
                "WebView2");

            Directory.CreateDirectory(userDataFolder);

            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: new CoreWebView2EnvironmentOptions());

            _browserReady = true;

            await AddTabAsync(select: true, initialUri: null);
            _panda?.Play(PandaMood.Success);
        }
        catch (Exception ex)
        {
            _panda?.Play(PandaMood.Error);
            MessageBox.Show(
                "Orvian konnte die WebView2-Browserengine nicht starten.\n\n" + ex.Message,
                "Orvian – Startfehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task AddTabAsync(bool select, string? initialUri)
    {
        if (!_browserReady || _environment == null) return;

        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DefaultBackgroundColor = System.Drawing.Color.White
        };

        var header = BuildTabHeader();
        var tab = new BrowserTab
        {
            View = view,
            HeaderButton = header
        };

        header.Tag = tab;
        HookWebView(view);
        _tabs.Add(tab);

        try
        {
            await view.EnsureCoreWebView2Async(_environment);
            ConfigureCore(view.CoreWebView2);

            if (select)
                SelectTab(tab);

            if (string.IsNullOrWhiteSpace(initialUri))
                await NavigateInternalAsync(tab, NewTabAddress, "Neuer Tab", NewTabHtml);
            else
                NavigateExternal(tab, initialUri);
        }
        catch (Exception ex)
        {
            _tabs.Remove(tab);
            try { view.Dispose(); } catch { }

            MessageBox.Show(
                "Der neue Tab konnte nicht gestartet werden.\n\n" + ex.Message,
                "Orvian",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private Button BuildTabHeader()
    {
        var close = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            Margin = new Thickness(5, 0, 0, 0),
            ToolTip = "Tab schließen"
        };
        close.Click += CloseTab_Click;

        var title = new TextBlock
        {
            Text = "Neuer Tab",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180,
            FontWeight = FontWeights.SemiBold
        };

        var icon = new Border
        {
            Width = 21,
            Height = 21,
            CornerRadius = new CornerRadius(11),
            Background = (Brush)FindResource("AccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "O",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        layout.Children.Add(icon);
        Grid.SetColumn(title, 1);
        layout.Children.Add(title);
        Grid.SetColumn(close, 2);
        layout.Children.Add(close);

        var button = new Button
        {
            Style = (Style)FindResource("TabHeaderButton"),
            Content = layout
        };
        button.Click += TabHeader_Click;

        return button;
    }

    private void TabHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is BrowserTab tab)
        {
            SelectTab(tab);
            e.Handled = true;
        }
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
            tab.HeaderButton.Background = tab == _activeTab
                ? (Brush)FindResource("AccentSoftBrush")
                : Brushes.White;

            tab.HeaderButton.BorderBrush = tab == _activeTab
                ? (Brush)FindResource("AccentBrush")
                : new SolidColorBrush(Color.FromRgb(212, 221, 232));
        }
    }

    private void UpdateTabTitle(BrowserTab tab, string title)
    {
        tab.Title = string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title;

        if (tab.HeaderButton.Content is Grid grid &&
            grid.Children.OfType<TextBlock>().FirstOrDefault() is { } titleBlock)
        {
            titleBlock.Text = tab.Title;
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button close ||
            close.Parent is not Grid grid ||
            grid.Parent is not Button header ||
            header.Tag is not BrowserTab tab)
        {
            return;
        }

        e.Handled = true;

        var index = _tabs.IndexOf(tab);
        if (index < 0) return;

        var wasActive = tab == _activeTab;
        _tabs.RemoveAt(index);

        try { tab.View.Dispose(); } catch { }

        if (_tabs.Count == 0)
        {
            _activeTab = null;
            _ = AddTabAsync(select: true, initialUri: null);
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
        foreach (var tab in _tabs)
            TabStrip.Children.Add(tab.HeaderButton);

        UpdateTabVisuals();
    }

    private void HookWebView(WebView2 view)
    {
        view.CoreWebView2InitializationCompleted += CoreWebView2InitializationCompleted;
    }

    private void CoreWebView2InitializationCompleted(
        object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
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

        try
        {
            core.AddWebResourceRequestedFilter(
                "*",
                CoreWebView2WebResourceContext.All,
                CoreWebView2WebResourceRequestSourceKinds.All);
        }
        catch { }

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
        => sender switch
        {
            CoreWebView2 core => TabFor(core),
            WebView2 view => _tabs.FirstOrDefault(x => ReferenceEquals(x.View, view)),
            _ => null
        };

    private async Task WarmupProtectionAsync()
    {
        await _blocker.RefreshFiltersAsync();

        if (_browserReady)
            Dispatcher.Invoke(() => PrivacyButton.ToolTip = $"Datenschutz-Center • {_blocker.RuleCount:N0} Schutzregeln");
    }

    private void StartUpdateTimer()
    {
        _updateTimer?.Stop();
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(15)
        };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var current = _updateChecker.CurrentVersion;
            var update = await _updateChecker.GetLatestAsync();

            if (update == null || update.Version <= current)
                return;

            _pendingUpdate = update;

            if (!_welcomeVisible && UpdateOverlay.Visibility != Visibility.Visible)
                ShowPendingUpdate();
        }
        catch
        {
            // Update checks are opportunistic and must never break browsing.
        }
    }

    private void ShowPendingUpdate()
    {
        if (_pendingUpdate == null) return;

        var installerText = _pendingUpdate.HasInstaller
            ? "Der geprüfte Installer ist bereits verfügbar."
            : "Der Release ist vorhanden, der Installer wird noch bereitgestellt.";

        UpdateText.Text =
            $"Orvian {_updateChecker.CurrentVersion} ist installiert. " +
            $"Version {_pendingUpdate.Version} ist verfügbar. {installerText}";

        UpdateNowButton.Content =
            _pendingUpdate.HasInstaller ? "Jetzt aktualisieren" : "Release öffnen";

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
            try
            {
                Process.Start(new ProcessStartInfo(UpdateChecker.ReleasesPage)
                {
                    UseShellExecute = true
                });
            }
            catch { }

            return;
        }

        UpdateNowButton.IsEnabled = false;
        UpdateNowButton.Content = "Wird heruntergeladen …";

        try
        {
            var path = await _updateChecker.DownloadInstallerAsync(_pendingUpdate);
            if (path is null)
                throw new InvalidOperationException("Installer konnte nicht heruntergeladen werden.");

            _panda?.Play(PandaMood.Success);

            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateNowButton.IsEnabled = true;
            UpdateNowButton.Content = "Jetzt aktualisieren";
            _panda?.Play(PandaMood.Error);

            MessageBox.Show(
                "Das Update konnte nicht gestartet werden.\n\n" + ex.Message,
                "Orvian Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Core_NewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;

        if (string.IsNullOrWhiteSpace(e.Uri))
            return;

        _ = Dispatcher.BeginInvoke(async () =>
        {
            await AddTabAsync(select: true, initialUri: e.Uri);
        });
    }

    private void WebResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            // Never block the top-level document. This keeps normal websites and local HTML working.
            if (e.ResourceContext == CoreWebView2WebResourceContext.Document)
                return;

            if (!_blocker.ShouldBlock(e.Request.Uri))
                return;

            if (TabFor(sender)?.View.CoreWebView2 is { } core)
            {
                e.Response = core.Environment.CreateWebResourceResponse(
                    null,
                    403,
                    "Blocked by Orvian",
                    "Content-Type: text/plain; charset=utf-8");
            }
        }
        catch
        {
            // A bad resource must never break the page.
        }
    }

    private async void WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.TryGetWebMessageAsString();

        try
        {
            if (message.StartsWith("search:", StringComparison.Ordinal))
            {
                var query = message[7..].Trim();

                if (!string.IsNullOrWhiteSpace(query))
                    NavigateFromAddressValue(query);

                return;
            }

            switch (message)
            {
                case "settings":
                    Menu_Click(this, new RoutedEventArgs());
                    break;
                case "vault":
                    OpenVault();
                    break;
                case "history":
                    await OpenHistoryPageAsync();
                    break;
                case "bookmarks":
                    await OpenBookmarksPageAsync();
                    break;
                case "downloads":
                    OpenDownloadsPage();
                    break;
                case "privacy":
                    OpenPrivacyPage();
                    break;
                case "permissions":
                    await OpenPermissionsPageAsync();
                    break;
                case "newtab":
                    await AddTabAsync(select: true, initialUri: null);
                    break;
            }
        }
        catch
        {
            // Web content is untrusted input; ignore malformed messages.
        }
    }

    private WebView2? CurrentView() => _activeTab?.View;

    private void DownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            Directory.CreateDirectory(folder);

            var file = Path.GetFileName(e.ResultFilePath);
            if (string.IsNullOrWhiteSpace(file))
                file = "Orvian-Download";

            var target = Path.Combine(folder, file);
            var unique = target;
            var n = 2;

            while (File.Exists(unique))
            {
                unique = Path.Combine(
                    folder,
                    $"{Path.GetFileNameWithoutExtension(file)} ({n++}){Path.GetExtension(file)}");
            }

            e.ResultFilePath = unique;
            _downloads.Insert(0, (Path.GetFileName(unique), unique, DateTimeOffset.Now));
            _panda?.Play(PandaMood.Happy);
        }
        catch
        {
            // Let WebView2 keep its default download behavior on failure.
        }
    }

    private async void PermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs e)
    {
        try
        {
            var kind = e.PermissionKind.ToString();
            var parsed = Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ? uri : null;
            var origin = parsed?.GetLeftPart(UriPartial.Authority) ?? e.Uri;

            var allow = MessageBox.Show(
                $"{origin}\n\nDie Website möchte Zugriff auf: {kind}.\n\nZugriff erlauben?",
                "Orvian Berechtigung",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

            e.State = allow
                ? CoreWebView2PermissionState.Allow
                : CoreWebView2PermissionState.Deny;

            await _data.AddPermissionAsync(
                origin,
                kind,
                allow ? "Erlaubt" : "Abgelehnt");

            _panda?.Play(allow ? PandaMood.Success : PandaMood.Privacy);
        }
        catch
        {
            e.State = CoreWebView2PermissionState.Deny;
        }
    }

    private void NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        var tab = TabFor(sender);
        if (tab == null) return;

        var isInternalDocument =
            tab.InternalPage &&
            e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

        if (!isInternalDocument)
        {
            tab.InternalPage = false;
            tab.DisplayAddress = e.Uri;

            if (tab == _activeTab)
                AddressBox.Text = tab.DisplayAddress;
        }

        _panda?.Play(PandaMood.Loading);
    }

    private async void NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        var tab = TabFor(sender);
        if (tab == null) return;

        if (!e.IsSuccess)
        {
            _panda?.Play(PandaMood.Error);
            return;
        }

        if (tab.InternalPage)
        {
            _panda?.Play(PandaMood.Success);
            UpdateNavigationUi();
            return;
        }

        UpdateNavigationUi();

        var title = tab.View.CoreWebView2.DocumentTitle;
        UpdateTabTitle(tab, string.IsNullOrWhiteSpace(title) ? "Neuer Tab" : title);

        if (tab.View.Source != null)
            await _data.AddHistoryAsync(tab.DisplayAddress, tab.Title);

        _panda?.Play(PandaMood.Success);
    }

    private void UpdateNavigationUi()
    {
        var tab = _activeTab;
        if (tab == null) return;

        AddressBox.Text = tab.DisplayAddress;
        BackButton.IsEnabled = tab.View.CanGoBack;
        ForwardButton.IsEnabled = tab.View.CanGoForward;
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !_browserReady)
            return;

        NavigateFromAddressValue(AddressBox.Text);
        e.Handled = true;
    }

    private void NavigateFromAddressValue(string rawValue)
    {
        var value = rawValue.Trim();
        var view = CurrentView();

        if (string.IsNullOrWhiteSpace(value) || view?.CoreWebView2 == null || _activeTab == null)
            return;

        switch (value.ToLowerInvariant())
        {
            case "orvian://newtab":
                _ = NavigateInternalAsync(_activeTab, NewTabAddress, "Neuer Tab", NewTabHtml);
                return;
            case "orvian://history":
                _ = OpenHistoryPageAsync();
                return;
            case "orvian://bookmarks":
                _ = OpenBookmarksPageAsync();
                return;
            case "orvian://downloads":
                OpenDownloadsPage();
                return;
            case "orvian://privacy":
                OpenPrivacyPage();
                return;
            case "orvian://permissions":
                _ = OpenPermissionsPageAsync();
                return;
            case "orvian://vault":
                OpenVault();
                return;
        }

        if (value.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("<body", StringComparison.OrdinalIgnoreCase))
        {
            NavigateExternal(
                _activeTab,
                "data:text/html;charset=utf-8," + Uri.EscapeDataString(value));
            return;
        }

        if (Path.IsPathRooted(value))
        {
            try
            {
                var fullPath = Path.GetFullPath(value);

                if (File.Exists(fullPath))
                {
                    NavigateExternal(_activeTab, new Uri(fullPath).AbsoluteUri);
                    return;
                }
            }
            catch { }
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            IsAllowedNavigationScheme(absolute.Scheme))
        {
            NavigateExternal(_activeTab, value);
            return;
        }

        if (Uri.TryCreate("http://" + value, UriKind.Absolute, out var local) &&
            (local.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             local.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)))
        {
            NavigateExternal(_activeTab, local.ToString());
            return;
        }

        view.CoreWebView2.Navigate(
            "https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    private static bool IsAllowedNavigationScheme(string scheme)
        => scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
           scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
           scheme.Equals("file", StringComparison.OrdinalIgnoreCase) ||
           scheme.Equals("data", StringComparison.OrdinalIgnoreCase) ||
           scheme.Equals("about", StringComparison.OrdinalIgnoreCase);

    private void NavigateExternal(BrowserTab tab, string uri)
    {
        if (tab.View.CoreWebView2 == null || string.IsNullOrWhiteSpace(uri))
            return;

        tab.InternalPage = false;
        tab.DisplayAddress = uri;

        if (tab == _activeTab)
            AddressBox.Text = uri;

        tab.View.CoreWebView2.Navigate(uri);
    }

    private async Task NavigateInternalAsync(
        BrowserTab tab,
        string address,
        string title,
        string html)
    {
        if (tab.View.CoreWebView2 == null)
            return;

        tab.InternalPage = true;
        tab.DisplayAddress = address;
        UpdateTabTitle(tab, title);

        if (tab == _activeTab)
            AddressBox.Text = address;

        tab.View.CoreWebView2.NavigateToString(html);
        await Task.CompletedTask;
    }

    private Task OpenNewTabPage()
    {
        if (_activeTab == null)
            return Task.CompletedTask;

        return NavigateInternalAsync(_activeTab, NewTabAddress, "Neuer Tab", NewTabHtml);
    }

    private string HtmlPage(string title, string body) => $$"""
        <!doctype html>
        <html lang="de">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>{{WebUtility.HtmlEncode(title)}}</title>
          <style>
            *{box-sizing:border-box}
            body{margin:0;font-family:Segoe UI,Arial,sans-serif;background:linear-gradient(135deg,#f7f9fc,#edf5ff);color:#182235;padding:42px}
            .wrap{max-width:1080px;margin:auto}
            h1{font-size:42px;margin:8px 0}
            p{color:#68758b;font-size:16px}
            .list{display:grid;gap:10px;margin-top:22px}
            .row{display:block;padding:16px 18px;background:#fff;border:1px solid #d8e2ed;border-radius:16px;text-decoration:none;color:#182235}
            .row span{display:block;color:#6d7a8e;font-size:13px;margin-top:5px}
            .empty{padding:22px;background:#fff;border:1px dashed #cbd6e3;border-radius:16px;color:#748095}
            .stats{display:flex;gap:14px;margin-top:22px}
            .stats>div{flex:1;background:#fff;border:1px solid #d8e2ed;border-radius:18px;padding:20px}
            .stats b,.stats span{display:block}
            .stats b{font-size:25px;color:#2f6bff}
            .stats span{margin-top:5px;color:#6d7a8e}
          </style>
        </head>
        <body><div class="wrap">{{body}}</div></body>
        </html>
        """;

    private async Task OpenHistoryPageAsync()
    {
        if (_activeTab == null) return;

        var items = await _data.GetHistoryAsync();
        var rows = new StringBuilder();

        foreach (var item in items.Take(80))
        {
            rows.Append(
                $"<a class='row' href='{WebUtility.HtmlEncode(item.Url)}'>" +
                $"<b>{WebUtility.HtmlEncode(item.Title)}</b>" +
                $"<span>{WebUtility.HtmlEncode(item.Url)} • {item.VisitedAt:dd.MM.yyyy HH:mm}</span>" +
                "</a>");
        }

        await NavigateInternalAsync(
            _activeTab,
            "orvian://history",
            "Verlauf",
            HtmlPage(
                "Verlauf",
                $"<h1>🕘 Verlauf</h1><p>Deine letzten Seiten – lokal in Orvian gespeichert.</p>" +
                $"<div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch kein Verlauf vorhanden.</div>" : rows.ToString())}</div>"));
    }

    private async Task OpenBookmarksPageAsync()
    {
        if (_activeTab == null) return;

        var items = await _data.GetBookmarksAsync();
        var rows = new StringBuilder();

        foreach (var item in items)
        {
            rows.Append(
                $"<a class='row' href='{WebUtility.HtmlEncode(item.Url)}'>" +
                $"<b>★ {WebUtility.HtmlEncode(item.Title)}</b>" +
                $"<span>{WebUtility.HtmlEncode(item.Url)}</span>" +
                "</a>");
        }

        await NavigateInternalAsync(
            _activeTab,
            "orvian://bookmarks",
            "Lesezeichen",
            HtmlPage(
                "Lesezeichen",
                $"<h1>★ Lesezeichen</h1><p>Deine gespeicherten Seiten.</p>" +
                $"<div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Lesezeichen.</div>" : rows.ToString())}</div>"));
    }

    private void OpenDownloadsPage()
    {
        if (_activeTab == null) return;

        var rows = new StringBuilder();

        foreach (var item in _downloads)
        {
            rows.Append(
                $"<div class='row'><b>📥 {WebUtility.HtmlEncode(item.FileName)}</b>" +
                $"<span>{WebUtility.HtmlEncode(item.Path)} • gestartet {item.StartedAt:HH:mm}</span></div>");
        }

        _ = NavigateInternalAsync(
            _activeTab,
            "orvian://downloads",
            "Downloads",
            HtmlPage(
                "Downloads",
                $"<h1>📥 Downloads</h1><p>Download-Historie dieser Sitzung.</p>" +
                $"<div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Downloads.</div>" : rows.ToString())}</div>"));
    }

    private void OpenPrivacyPage()
    {
        if (_activeTab == null) return;

        var blocked = Math.Max(0, _blocker.RuleCount);

        _ = NavigateInternalAsync(
            _activeTab,
            "orvian://privacy",
            "Datenschutz",
            HtmlPage(
                "Datenschutz",
                $"<h1>🛡 Datenschutz-Center</h1>" +
                $"<p>Orvian lässt normale HTML-Dokumente und Websites durch und filtert nur erkannte Netzwerkressourcen innerhalb geladener Seiten.</p>" +
                $"<div class='stats'>" +
                $"<div><b>{blocked:N0}</b><span>geladene Schutzregeln</span></div>" +
                $"<div><b>Aktiv</b><span>Netzwerk-Filter</span></div>" +
                $"<div><b>Lokal</b><span>Browserdaten</span></div>" +
                $"</div>"));
    }

    private async Task OpenPermissionsPageAsync()
    {
        if (_activeTab == null) return;

        var items = await _data.GetPermissionsAsync();
        var rows = new StringBuilder();

        foreach (var item in items)
        {
            rows.Append(
                $"<div class='row'><b>{WebUtility.HtmlEncode(item.Origin)}</b>" +
                $"<span>{WebUtility.HtmlEncode(item.Permission)} • {WebUtility.HtmlEncode(item.Decision)}</span></div>");
        }

        await NavigateInternalAsync(
            _activeTab,
            "orvian://permissions",
            "Berechtigungen",
            HtmlPage(
                "Berechtigungen",
                $"<h1>🔐 Berechtigungen</h1><p>Entscheidungen werden lokal gespeichert.</p>" +
                $"<div class='list'>{(rows.Length == 0 ? "<div class='empty'>Noch keine Berechtigungen gespeichert.</div>" : rows.ToString())}</div>"));
    }

    private void OpenVault()
    {
        new PasswordVaultWindow(_vault)
        {
            Owner = this
        }.ShowDialog();
    }

    private void NavigateToCommand(string id)
    {
        switch (id)
        {
            case "newtab":
                _ = AddTabAsync(select: true, initialUri: null);
                break;
            case "history":
                _ = OpenHistoryPageAsync();
                break;
            case "bookmarks":
                _ = OpenBookmarksPageAsync();
                break;
            case "downloads":
                OpenDownloadsPage();
                break;
            case "privacy":
                OpenPrivacyPage();
                break;
            case "permissions":
                _ = OpenPermissionsPageAsync();
                break;
            case "settings":
                Menu_Click(this, new RoutedEventArgs());
                break;
            case "vault":
                OpenVault();
                break;
            case "update":
                _ = ForceUpdateCheckAsync();
                break;
            case "reload":
                CurrentView()?.Reload();
                break;
            case "focus":
                AddressBox.Focus();
                AddressBox.SelectAll();
                break;
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

                MessageBox.Show(
                    $"Du verwendest bereits Orvian {_updateChecker.CurrentVersion}.",
                    "Orvian Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            _pendingUpdate = update;
            ShowPendingUpdate();
        }
        catch (Exception ex)
        {
            _panda?.Play(PandaMood.Error);

            MessageBox.Show(
                "Die Update-Prüfung ist momentan nicht erreichbar.\n\n" + ex.Message,
                "Orvian Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentView()?.CanGoBack == true)
            CurrentView()!.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentView()?.CanGoForward == true)
            CurrentView()!.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
        => CurrentView()?.Reload();

    private void Home_Click(object sender, RoutedEventArgs e)
        => _ = OpenNewTabPage();

    private void NewTab_Click(object sender, RoutedEventArgs e)
        => _ = AddTabAsync(select: true, initialUri: null);

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        var view = CurrentView();

        if (view?.Source == null || _activeTab?.InternalPage == true)
            return;

        await _data.ToggleBookmarkAsync(
            view.Source.ToString(),
            view.CoreWebView2.DocumentTitle);

        _panda?.Play(PandaMood.Success);
    }

    private void Privacy_Click(object sender, RoutedEventArgs e)
    {
        OpenPrivacyPage();
        _panda?.Play(PandaMood.Privacy);
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
        => new SettingsWindow { Owner = this }.ShowDialog();

    private async void CheckPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordPrompt { Owner = this };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var result = await PasswordSecurity.CheckPwnedAsync(dialog.Password);
            MessageBox.Show(result, "Orvian Passwortschutz");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Die Passwortprüfung konnte nicht abgeschlossen werden.\n\n" + ex.Message,
                "Orvian Passwortschutz",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            dialog.Close();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (e.Key == Key.C)
            {
                LockBrowser();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D)
            {
                UnlockBrowser();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        switch (e.Key)
        {
            case Key.K:
                ShowCommandPalette();
                e.Handled = true;
                break;
            case Key.L:
                AddressBox.Focus();
                AddressBox.SelectAll();
                e.Handled = true;
                break;
            case Key.T:
                _ = AddTabAsync(select: true, initialUri: null);
                e.Handled = true;
                break;
            case Key.W:
                CloseActiveTab();
                e.Handled = true;
                break;
            case Key.H:
                _ = OpenHistoryPageAsync();
                e.Handled = true;
                break;
            case Key.J:
                OpenDownloadsPage();
                e.Handled = true;
                break;
            case Key.R:
                CurrentView()?.Reload();
                e.Handled = true;
                break;
            case Key.D:
                _ = Bookmark_ClickAsync();
                e.Handled = true;
                break;
        }
    }

    private void CloseActiveTab()
    {
        if (_activeTab?.HeaderButton is not { } header ||
            header.Content is not Grid grid ||
            grid.Children.OfType<Button>().FirstOrDefault() is not { } close)
        {
            return;
        }

        CloseTab_Click(close, new RoutedEventArgs());
    }

    private async Task Bookmark_ClickAsync()
    {
        var view = CurrentView();

        if (view?.Source == null || _activeTab?.InternalPage == true)
            return;

        await _data.ToggleBookmarkAsync(
            view.Source.ToString(),
            view.CoreWebView2.DocumentTitle);

        _panda?.Play(PandaMood.Success);
    }

    private void ShowCommandPalette()
    {
        var dialog = new CommandPaletteWindow(Commands)
        {
            Owner = this
        };

        dialog.CommandInvoked += (_, id) =>
            Dispatcher.BeginInvoke(() => NavigateToCommand(id));

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

    private void Unlock_Click(object sender, RoutedEventArgs e)
        => UnlockBrowser();
}
