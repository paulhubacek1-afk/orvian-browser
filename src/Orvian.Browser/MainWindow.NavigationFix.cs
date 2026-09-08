using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow
{
    private static readonly HashSet<WebView2> NavigationBridgeViews = new();
    private static readonly HashSet<CoreWebView2> NavigationBridgeCores = new();

    private static readonly bool NavigationFixRegistered = RegisterNavigationFix();

    private static bool RegisterNavigationFix()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnNavigationFixMainWindowLoaded),
            true);

        EventManager.RegisterClassHandler(
            typeof(WebView2),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnNavigationFixWebViewLoaded),
            true);

        return true;
    }

    private static void OnNavigationFixMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            window._blocker.AllowSite("google.com");
            window._blocker.AllowSite("google.de");
            window._blocker.AllowSite("gstatic.com");
            window._blocker.AllowSite("googleapis.com");
            window._blocker.AllowSite("youtube.com");
            window._blocker.AllowSite("ytimg.com");
            window._blocker.AllowSite("github.com");

            foreach (var button in FindVisualChildren<Button>(window))
            {
                if (button.Tag is string tag &&
                    tag.Equals("https://www.youtube.com/@Noctra11", StringComparison.OrdinalIgnoreCase))
                {
                    button.Tag = "https://www.youtube.com/@Noctra11_Yt";
                }
            }
        }));
    }

    private static void OnNavigationFixWebViewLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WebView2 view)
            return;

        lock (NavigationBridgeViews)
        {
            if (!NavigationBridgeViews.Add(view))
                return;
        }

        view.CoreWebView2InitializationCompleted += NavigationFixCoreInitialized;

        if (view.CoreWebView2 is not null)
            AttachNavigationBridge(view.CoreWebView2);
    }

    private static void NavigationFixCoreInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (sender is not WebView2 view || !e.IsSuccess || view.CoreWebView2 is null)
            return;

        AttachNavigationBridge(view.CoreWebView2);
    }

    private static void AttachNavigationBridge(CoreWebView2 core)
    {
        lock (NavigationBridgeCores)
        {
            if (!NavigationBridgeCores.Add(core))
                return;
        }

        core.NavigationCompleted += NavigationFixNavigationCompleted;
        core.WebMessageReceived += NavigationFixWebMessageReceived;
    }

    private static async void NavigationFixNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || sender is not CoreWebView2 core)
            return;

        try
        {
            var source = core.Source ?? string.Empty;
            if (!source.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase) &&
                !source.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
                return;

            await core.ExecuteScriptAsync(@"
(() => {
    document.querySelectorAll('button.card').forEach(button => {
        if (button.dataset.orvianNavigationBridge === '1') return;

        const onclick = button.getAttribute('onclick') || '';
        const match = onclick.match(/go\\(['\"']([^'\"']+)['\"']\\)/);
        if (!match) return;

        const url = match[1];
        button.removeAttribute('onclick');
        button.dataset.orvianNavigationBridge = '1';
        button.addEventListener('click', event => {
            event.preventDefault();
            event.stopImmediatePropagation();
            window.chrome?.webview?.postMessage('open:' + url);
        }, true);
    });
})();");
        }
        catch
        {
        }
    }

    private static void NavigationFixWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (sender is not CoreWebView2 core)
            return;

        string? message;
        try
        {
            message = e.TryGetWebMessageAsString();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message) ||
            !message.StartsWith("open:", StringComparison.Ordinal))
            return;

        var url = message[5..].Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
             !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            return;

        foreach (Window openWindow in Application.Current.Windows)
        {
            if (openWindow is not MainWindow window)
                continue;

            var tab = window._tabs.FirstOrDefault(
                x => ReferenceEquals(x.View.CoreWebView2, core));

            if (tab is null)
                continue;

            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.NavigateExternal(tab, uri.AbsoluteUri);
            }), DispatcherPriority.Input);
            return;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
