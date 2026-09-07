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
    private const string Noctra11YouTubeUrl = "https://www.youtube.com/@Noctra11";
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

        if (!_welcomeVisible)
            Dispatcher.BeginInvoke(new Action(ShowCreatorBanner), DispatcherPriority.ApplicationIdle);

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

    private void ShowCreatorBanner()
    {
        if (_creatorBannerVisible || _welcomeVisible || CreatorOverlay.Visibility == Visibility.Visible)
            return;

        _creatorBannerVisible = true;
        CreatorOverlay.Visibility = Visibility.Visible;
        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("CreatorIntro"));
        _panda?.Play(PandaMood.Happy);
    }

    private async void CreatorBannerLater_Click(object sender, RoutedEventArgs e)
    {
        await CloseCreatorBannerAsync();
    }

    private async void CreatorLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string url)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            await CloseCreatorBannerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Der Creator-Link konnte nicht geöffnet werden.\n\n" + ex.Message,
                "Orvian",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task CloseCreatorBannerAsync()
    {
        if (!_creatorBannerVisible) return;

        BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("CreatorOutro"));
        _panda?.Play(PandaMood.Success);
        await Task.Delay(180);
        CreatorOverlay.Visibility = Visibility.Collapsed;
        _creatorBannerVisible = false;

        if (_pendingUpdate != null)
            ShowPendingUpdate();
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

        ShowCreatorBanner();
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
        TabStrip.Children.Add(header);

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
            TabStrip.Children.Remove(header);
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
        CloseTab(tab);
    }

    private void CloseTab(BrowserTab tab)
    {
        if (!_tabs.Remove(tab)) return;

        TabStrip.Children.Remove(tab.HeaderButton);
        try { tab.View.Dispose(); } catch { }

        if (_tabs.Count == 0)
        {
            _ = AddTabAsync(select: true, initialUri: null);
            return;
        }

        if (_activeTab == tab)
            SelectTab(_tabs[Math.Clamp(_tabs.Count - 1, 0, _tabs.Count - 1)]);
    }
