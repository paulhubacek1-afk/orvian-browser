using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow : Window
{
    private readonly Blocker _blocker = new();
    private readonly PasswordVault _vault = new();
    private bool _locked;
    private const string Home = "https://www.google.com/";

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await BrowserView.EnsureCoreWebView2Async();
        BrowserView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        BrowserView.CoreWebView2.WebResourceRequested += WebResourceRequested;
        BrowserView.CoreWebView2.NavigationStarting += NavigationStarting;
        BrowserView.CoreWebView2.NavigationCompleted += NavigationCompleted;
        BrowserView.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            BrowserView.CoreWebView2.Navigate(args.Uri);
        };
        BrowserView.CoreWebView2.Navigate(Home);
    }

    private void WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_blocker.ShouldBlock(e.Request.Uri))
        {
            e.Response = BrowserView.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked by Orvian", "Content-Type: text/plain");
        }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_blocker.IsBlockedHost(e.Uri))
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => AddressBox.Text = "orvian://blocked");
        }
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        AddressBox.Text = BrowserView.Source?.ToString() ?? Home;
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var value = AddressBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        var url = value.Contains(" ") ? "https://www.google.com/search?q=" + Uri.EscapeDataString(value) :
            (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? value : "https://" + value);
        BrowserView.CoreWebView2.Navigate(url);
        e.Handled = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (BrowserView.CanGoBack) BrowserView.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (BrowserView.CanGoForward) BrowserView.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) { BrowserView.Reload(); }
    private void Menu_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Einstellungen folgen: Farben, Filter, Shortcuts, Datenschutz und Websites als App.", "Orvian");
    private void InstallApp_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Web-App-Installation wird in der nächsten Ausbaustufe mit Manifest-Erkennung umgesetzt.", "Orvian");

    private async void CheckPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordPrompt();
        if (dialog.ShowDialog() != true) return;
        var result = await PasswordSecurity.CheckPwnedAsync(dialog.Password);
        MessageBox.Show(result, "Orvian Passwortschutz");
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.C)
        {
            LockBrowser();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.D)
        {
            UnlockBrowser();
            e.Handled = true;
        }
    }

    private void LockBrowser()
    {
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
        // First implementation: local UI lock. Replace with the configured vault PIN/password verifier.
        if (!string.IsNullOrEmpty(UnlockBox.Password)) UnlockBrowser();
    }
}