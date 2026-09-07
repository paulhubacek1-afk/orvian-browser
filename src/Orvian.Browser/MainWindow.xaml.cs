using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow : Window
{
    private readonly Blocker _blocker = new();
    private readonly UpdateChecker _updateChecker = new();
    private bool _locked;
    private bool _browserReady;
    private bool _welcomeVisible;
    private UpdateInfo? _pendingUpdate;
    private DispatcherTimer? _updateTimer;
    private const string Home = "https://www.google.com/";
    private static readonly string WelcomeFlag = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "welcome-shown.flag");

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
        _ = InitializeBrowserAsync();
        _ = WarmupProtectionAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e) => _updateTimer?.Stop();

    private void BeginIntro() => BeginStoryboard((Storyboard)FindResource("Intro"));

    private void ShowWelcomeIfNeeded()
    {
        try
        {
            if (File.Exists(WelcomeFlag)) return;
            _welcomeVisible = true;
            WelcomeOverlay.Visibility = Visibility.Visible;
            BeginStoryboard((Storyboard)FindResource("WelcomeIntro"));
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

        BeginStoryboard((Storyboard)FindResource("WelcomeOutro"));
        await Task.Delay(280);
        WelcomeOverlay.Visibility = Visibility.Collapsed;
        _welcomeVisible = false;
        _ = CheckForUpdatesAsync();
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browserReady) return;
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "WebView2");
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

            _browserReady = true;
            PrivacyStats.Text = "Orvian schützt deine Sitzung • WebView2 bereit";
            core.Navigate(Home);
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            PrivacyStats.Text = "Start der Browser-Engine fehlgeschlagen";
            MessageBox.Show(
                "Orvian konnte die WebView2-Browserengine nicht starten.\n\n" + ex.Message +
                "\n\nInstalliere die aktuelle Microsoft Edge WebView2 Runtime und starte Orvian erneut.",
                "Orvian – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
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
            BeginStoryboard((Storyboard)FindResource("UpdateIntro"));
        }
        catch { }
    }

    private void UpdateLater_Click(object sender, RoutedEventArgs e)
    {
        UpdateOverlay.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is null) return;
        if (sender is Button button) { button.IsEnabled = false; button.Content = "Wird heruntergeladen …"; }
        try
        {
            var path = await _updateChecker.DownloadInstallerAsync(_pendingUpdate);
            if (path is null) throw new InvalidOperationException("Installer konnte nicht heruntergeladen werden.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
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

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        PrivacyStats.Text = "Orvian lädt die Seite …";
        if (_blocker.IsBlockedHost(e.Uri))
        {
            e.Cancel = true;
            AddressBox.Text = "orvian://blocked";
        }
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (BrowserView.Source != null) AddressBox.Text = BrowserView.Source.ToString();
        BackButton.IsEnabled = BrowserView.CanGoBack;
        ForwardButton.IsEnabled = BrowserView.CanGoForward;
        PrivacyStats.Text = e.IsSuccess ? "Orvian schützt deine Sitzung • Seite bereit" : "Orvian • Seite konnte nicht vollständig geladen werden";
        TabTitle.Text = BrowserView.CoreWebView2.DocumentTitle;
        if (string.IsNullOrWhiteSpace(TabTitle.Text)) TabTitle.Text = "Neuer Tab";
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
        if (string.IsNullOrWhiteSpace(value)) return;
        if (TryBuildNetworkUrl(value, out var url))
        {
            BrowserView.CoreWebView2.Navigate(url);
            return;
        }
        BrowserView.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    private static bool TryBuildNetworkUrl(string value, out string url)
    {
        url = string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || absolute.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            url = absolute.ToString();
            return true;
        }

        if (IPAddress.TryParse(value, out var directIp))
        {
            url = directIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"http://[{value}]/"
                : $"http://{value}/";
            return true;
        }

        if (Uri.TryCreate("http://" + value, UriKind.Absolute, out var localOrIp) &&
            (localOrIp.HostNameType == UriHostNameType.IPv4 || localOrIp.HostNameType == UriHostNameType.IPv6))
        {
            url = localOrIp.ToString();
            return true;
        }

        if (value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + value;
            return true;
        }

        if (Uri.TryCreate("https://" + value, UriKind.Absolute, out var domain) && !string.IsNullOrWhiteSpace(domain.Host))
        {
            url = domain.ToString();
            return true;
        }
        return false;
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (_browserReady && BrowserView.CanGoBack) BrowserView.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (_browserReady && BrowserView.CanGoForward) BrowserView.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) { if (_browserReady) BrowserView.Reload(); }
    private void Home_Click(object sender, RoutedEventArgs e) { if (_browserReady) BrowserView.CoreWebView2.Navigate(Home); }
    private void NewTab_Click(object sender, RoutedEventArgs e) { TabTitle.Text = "Neuer Tab"; AddressBox.Text = Home; if (_browserReady) BrowserView.CoreWebView2.Navigate(Home); }
    private void CloseTab_Click(object sender, RoutedEventArgs e) => NewTab_Click(sender, e);
    private void Tab_Click(object sender, MouseButtonEventArgs e) { if (_browserReady) BrowserView.Focus(); }
    private void Bookmark_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Lesezeichen werden lokal gespeichert und in der nächsten Ausbaustufe in einer eigenen Bibliothek angezeigt.", "Orvian");
    private void Privacy_Click(object sender, RoutedEventArgs e) => MessageBox.Show($"Werbe-/Tracker-Schutz ist aktiv. {Math.Max(0, _blocker.RuleCount):N0} Regeln sind geladen.", "Orvian Datenschutz");
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
        if (Keyboard.Modifiers != ModifierKeys.Alt) return;
        if (e.Key == Key.C) { LockBrowser(); e.Handled = true; }
        else if (e.Key == Key.D) { UnlockBrowser(); e.Handled = true; }
    }

    private void LockBrowser()
    {
        if (_locked) return;
        _locked = true;
        BrowserView.Visibility = Visibility.Hidden;
        LockOverlay.Visibility = Visibility.Visible;
        UnlockBox.Clear();
        UnlockBox.Focus();
    }

    private void UnlockBrowser()
    {
        if (!_locked) return;
        _locked = false;
        LockOverlay.Visibility = Visibility.Collapsed;
        BrowserView.Visibility = Visibility.Visible;
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => UnlockBrowser();
}
