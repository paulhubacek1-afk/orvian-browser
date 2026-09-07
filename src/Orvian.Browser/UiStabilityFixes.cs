using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace Orvian.Browser;

// Small runtime fixes kept separate from the main window implementation so UI regressions
// can be corrected without duplicating the browser logic.
public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(MainWindowLoadedFix));
        EventManager.RegisterClassHandler(typeof(WebView2), FrameworkElement.LoadedEvent, new RoutedEventHandler(WebViewLoadedFix));
    }

    private static void MainWindowLoadedFix(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window) return;

        // The normal Windows title bar already owns minimize/maximize/close.
        // The old custom copy created a confusing second set of window controls.
        foreach (var panel in FindVisualChildren<StackPanel>(window))
        {
            var buttons = panel.Children.OfType<Button>().ToList();
            if (buttons.Any(b => string.Equals(b.Content?.ToString(), "—", StringComparison.Ordinal)) &&
                buttons.Any(b => string.Equals(b.Content?.ToString(), "□", StringComparison.Ordinal)))
            {
                foreach (var button in buttons) button.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static void WebViewLoadedFix(object sender, RoutedEventArgs e)
    {
        if (sender is not WebView2 view) return;

        view.CoreWebView2InitializationCompleted -= FixCoreInitialized;
        view.CoreWebView2InitializationCompleted += FixCoreInitialized;
        if (view.CoreWebView2 != null) AttachCoreFixes(view.CoreWebView2);
    }

    private static void FixCoreInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (sender is WebView2 view && e.IsSuccess && view.CoreWebView2 != null)
            AttachCoreFixes(view.CoreWebView2);
    }

    private static void AttachCoreFixes(CoreWebView2 core)
    {
        core.NavigationCompleted -= RestoreInternalChrome;
        core.NavigationCompleted += RestoreInternalChrome;
    }

    private static void RestoreInternalChrome(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (sender is not CoreWebView2 core) return;
        if (Application.Current?.MainWindow is not MainWindow window) return;

        var tab = window.TabFor(core);
        if (tab == null || !tab.InternalPage) return;

        // NavigateToString() internally uses a data: document. Never expose that implementation
        // detail in the tab title or address bar.
        window.UpdateTabTitle(tab, tab.Title);
        if (tab == window._activeTab)
            window.AddressBox.Text = tab.Title switch
            {
                "Neuer Tab" => "orvian://newtab",
                "Verlauf" => "orvian://history",
                "Lesezeichen" => "orvian://bookmarks",
                "Downloads" => "orvian://downloads",
                "Datenschutz" => "orvian://privacy",
                "Berechtigungen" => "orvian://permissions",
                _ => window.AddressBox.Text
            };
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) yield break;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }
}

public partial class SettingsWindow
{
    static SettingsWindow()
    {
        EventManager.RegisterClassHandler(typeof(SettingsWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(SettingsLoadedFix));
    }

    private static void SettingsLoadedFix(object sender, RoutedEventArgs e)
    {
        if (sender is not SettingsWindow window) return;

        // Give the navigation card enough room so the footer text never wraps one character per line.
        foreach (var border in FindVisualChildren<Border>(window))
        {
            if (border.Child is TextBlock text && text.Text.StartsWith("Änderungen gelten lokal", StringComparison.Ordinal))
            {
                border.Width = 190;
                border.HorizontalAlignment = HorizontalAlignment.Center;
                text.TextWrapping = TextWrapping.Wrap;
            }
        }

        // More detailed Chromium-inspired project description.
        foreach (var text in FindVisualChildren<TextBlock>(window))
        {
            if (text.Text.StartsWith("Orvian kombiniert WebView2", StringComparison.Ordinal))
            {
                text.Text = "Orvian Browser ist ein moderner, auf Microsoft Edge WebView2 basierender Desktop-Browser für Windows. Die Browseroberfläche verbindet Tabs, Navigation, Downloads, Lesezeichen, Verlauf und Web-App-Unterstützung mit einem integrierten Datenschutz- und Sicherheitskonzept. Dazu gehören ein lokaler Tracker- und Werbeblocker, ein verschlüsselter Passwort-Tresor, Berechtigungsverwaltung sowie automatische Update-Prüfungen. Orvian wurde mit dem Anspruch entwickelt, eine vertraute Chromium-nahe Browsererfahrung mit eigener Oberfläche, eigenen Funktionen und dem animierten Orvian-Panda als persönlicher Begleitung zu verbinden.";
                text.MaxWidth = 700;
                break;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) yield break;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }
}
