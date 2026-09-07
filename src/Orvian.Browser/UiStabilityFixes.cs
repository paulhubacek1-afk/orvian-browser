using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Orvian.Browser;

// Small runtime polish layer for the browser UI. It intentionally lives outside the
// main window implementation so visual fixes do not complicate navigation logic.
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

        // The native Windows title bar already owns close/minimize/maximize.
        // The redesigned XAML no longer renders duplicate window buttons; this also
        // protects existing installations whose XAML is still cached during an update.
        foreach (var panel in FindVisualChildren<StackPanel>(window))
        {
            var buttons = panel.Children.OfType<Button>().ToList();
            if (buttons.Any(b => b.Content?.ToString() == "—") && buttons.Any(b => b.Content?.ToString() == "□"))
                foreach (var button in buttons) button.Visibility = Visibility.Collapsed;
        }

        // Check often enough to feel live, while keeping network usage tiny.
        window._updateTimer?.Stop();
        window._updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        window._updateTimer.Tick += async (_, _) => await window.CheckForUpdatesAsync();
        window._updateTimer.Start();
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

    private static async void RestoreInternalChrome(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (sender is not CoreWebView2 core || !e.IsSuccess) return;
        if (Application.Current?.MainWindow is not MainWindow window) return;

        var tab = window.TabFor(core);
        if (tab == null || !tab.InternalPage) return;

        // NavigateToString creates an implementation-level data: document. Never show that
        // implementation detail to the user.
        window.UpdateTabTitle(tab, tab.Title);
        if (tab == window._activeTab)
        {
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

        if (tab.Title == "Neuer Tab")
        {
            const string script = """
                (() => {
                    document.querySelector('.panda')?.remove();
                    const oldStyle = document.getElementById('orvian-modern-style');
                    if (oldStyle) oldStyle.remove();
                    const style = document.createElement('style');
                    style.id = 'orvian-modern-style';
                    style.textContent = `
                        *{box-sizing:border-box}
                        body{margin:0;min-height:100vh;font-family:Segoe UI,Arial,sans-serif;color:#172236;background:radial-gradient(circle at 85% 8%,#dcebff 0,#f7faff 34%,#edf4ff 100%);overflow-x:hidden}
                        body:before{content:'';position:fixed;inset:-20%;background:radial-gradient(circle at 20% 85%,rgba(47,107,255,.10),transparent 32%),radial-gradient(circle at 70% 55%,rgba(114,190,255,.12),transparent 30%);pointer-events:none}
                        main{position:relative;max-width:1180px;margin:0 auto;padding:74px 56px 76px}
                        .hero{display:grid;grid-template-columns:84px 1fr;gap:24px;align-items:center;margin:20px 0 34px}
                        .hero:before{content:'O';display:flex;align-items:center;justify-content:center;width:84px;height:84px;border-radius:28px;background:linear-gradient(145deg,#2f6bff,#6a9dff);color:white;font-size:44px;font-weight:800;box-shadow:0 18px 45px rgba(47,107,255,.28)}
                        .panda{display:none!important}.eyebrow{font-size:11px;letter-spacing:.2em;font-weight:800;color:#2f6bff}.hero h1{font-size:52px;line-height:1.02;margin:6px 0 12px;letter-spacing:-.03em}.hero p{font-size:18px;color:#65738b;margin:0}
                        .search{display:flex;max-width:890px;background:rgba(255,255,255,.96);border:1px solid #cbd8e8;border-radius:24px;padding:8px;box-shadow:0 18px 50px rgba(51,83,125,.14);backdrop-filter:blur(10px)}
                        .search input{flex:1;border:0;outline:0;font-size:17px;padding:14px 16px;background:transparent}.search button{width:56px;border:0;border-radius:17px;background:#2f6bff;color:#fff;font-size:24px;cursor:pointer}
                        .grid{display:grid;grid-template-columns:repeat(3,1fr);gap:15px;margin-top:22px}.card{border:1px solid rgba(207,220,235,.95);background:rgba(255,255,255,.86);border-radius:20px;padding:21px;text-align:left;cursor:pointer;box-shadow:0 11px 30px rgba(40,63,95,.07);transition:transform .18s ease,box-shadow .18s ease,border-color .18s ease}.card:hover{transform:translateY(-4px);box-shadow:0 19px 42px rgba(40,63,95,.13);border-color:#b9cdf1}.card b,.card span{display:block}.card b{font-size:16px}.card span{color:#718096;font-size:13px;margin-top:6px}
                        .tip{margin-top:22px;padding:13px 16px;border:1px solid #d8e3ef;border-radius:14px;background:rgba(255,255,255,.6);color:#748096;font-size:13px;max-width:890px}
                        @media(max-width:900px){main{padding:44px 24px}.grid{grid-template-columns:1fr 1fr}.hero h1{font-size:40px}}
                        @media(max-width:650px){.grid{grid-template-columns:1fr}.hero{grid-template-columns:1fr}.hero:before{width:68px;height:68px;border-radius:22px;font-size:34px}.hero h1{font-size:34px}}
                    `;
                    document.head.appendChild(style);
                    const eyebrow=document.querySelector('.eyebrow'); if(eyebrow) eyebrow.textContent='ORVIAN BROWSER';
                    const title=document.querySelector('.hero h1'); if(title) title.textContent='Dein Browser. Dein Raum.';
                    const subtitle=document.querySelector('.hero p'); if(subtitle) subtitle.textContent='Schnell, privat und bewusst anders – mit einem Panda, der im Hintergrund auf dich aufpasst.';
                })();
                """;
            try { await core.ExecuteScriptAsync(script); } catch { }
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

public partial class SettingsWindow
{
    static SettingsWindow()
    {
        EventManager.RegisterClassHandler(typeof(SettingsWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(SettingsLoadedFix));
    }

    private static void SettingsLoadedFix(object sender, RoutedEventArgs e)
    {
        if (sender is not SettingsWindow window) return;
        foreach (var text in FindVisualChildren<TextBlock>(window))
        {
            if (text.Text.StartsWith("Orvian kombiniert WebView2", StringComparison.Ordinal))
            {
                text.MaxWidth = 760;
                text.TextWrapping = TextWrapping.Wrap;
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
