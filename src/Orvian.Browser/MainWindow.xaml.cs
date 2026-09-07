using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Orvian.Browser;

public partial class MainWindow : Window
{
    private readonly Blocker _blocker = new();
    private bool _locked;
    private bool _browserReady;
    private const string Home = "https://www.google.com/";

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_browserReady) return;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Orvian", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder,
                options: new CoreWebView2EnvironmentOptions());

            await BrowserView.EnsureCoreWebView2Async(environment);

            var core = BrowserView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;

            // Keep the blocker lightweight; it only inspects requests and never rewrites page content.
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
                CoreWebView2WebResourceRequestSourceKinds.All);
            core.WebResourceRequested += WebResourceRequested;
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.NewWindowRequested += Core_NewWindowRequested;

            _browserReady = true;
            PrivacyStats.Text = "Orvian ist bereit";
            core.Navigate(Home);
        }
        catch (Exception ex)
        {
            _browserReady = false;
            PrivacyStats.Text = "Browser konnte nicht gestartet werden";
            var message =
                "Orvian konnte die Browser-Engine nicht starten.\n\n" +
                "Prüfe, ob Microsoft Edge WebView2 Runtime installiert ist.\n\n" +
                "Technische Meldung:\n" + ex.Message;
            MessageBox.Show(message, "Orvian – Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (_browserReady && !string.IsNullOrWhiteSpace(e.Uri))
            BrowserView.CoreWebView2.Navigate(e.Uri);
    }

    private void WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            if (_blocker.ShouldBlock(e.Request.Uri))
            {
                e.Response = BrowserView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 403, "Blocked by Orvian", "Content-Type: text/plain");
            }
        }
        catch
        {
            // Never allow blocker errors to terminate navigation or the browser process.
        }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_blocker.IsBlockedHost(e.Uri))
        {
            e.Cancel = true;
            AddressBox.Text = "orvian://blocked";
        }
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (BrowserView.Source != null)
            AddressBox.Text = BrowserView.Source.ToString();

        BackButton.IsEnabled = BrowserView.CanGoBack;
        ForwardButton.IsEnabled = BrowserView.CanGoForward;
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !_browserReady) return;

        var value = AddressBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;

        var url = value.Contains(' ')
            ? "https://www.google.com/search?q=" + Uri.EscapeDataString(value)
            : value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? value
                : "https://" + value;

        BrowserView.CoreWebView2.Navigate(url);
        e.Handled = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady && BrowserView.CanGoBack) BrowserView.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady && BrowserView.CanGoForward) BrowserView.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady) BrowserView.Reload();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (_browserReady) BrowserView.CoreWebView2.Navigate(Home);
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
    }

    private void InstallApp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Web-Apps werden in einer späteren Version über Web-App-Manifeste als eigene Orvian-App angelegt.",
            "Orvian");
    }

    private async void CheckPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordPrompt { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var result = await PasswordSecurity.CheckPwnedAsync(dialog.Password);
        dialog.Close();
        MessageBox.Show(result, "Orvian Passwortschutz");
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt) return;

        if (e.Key == Key.C)
        {
            LockBrowser();
            e.Handled = true;
        }
        else if (e.Key == Key.D)
        {
            UnlockBrowser();
            e.Handled = true;
        }
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

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        // UI lock for this first release; password-vault authentication is separate from browsing.
        UnlockBrowser();
    }
}