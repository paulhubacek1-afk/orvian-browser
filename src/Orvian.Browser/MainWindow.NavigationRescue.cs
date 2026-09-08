using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow
{
    private static readonly string[] NavigationSafeHosts =
    [
        "google.com",
        "google.de",
        "gstatic.com",
        "googleapis.com",
        "googleusercontent.com",
        "youtube.com",
        "youtube-nocookie.com",
        "ytimg.com",
        "ggpht.com",
        "googlevideo.com",
        "github.com",
        "githubusercontent.com",
        "githubassets.com"
    ];

    private static readonly bool NavigationRescueRegistered = RegisterNavigationRescue();

    private static bool RegisterNavigationRescue()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnNavigationRescueLoaded),
            true);

        return true;
    }

    private static void OnNavigationRescueLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            foreach (var host in NavigationSafeHosts)
                window._blocker.AllowSite(host);

            // Canonical Noctra11 YouTube channel URL.
            foreach (var button in FindVisualChildren<Button>(window))
            {
                if (button.Tag is string tag &&
                    (tag.Equals("https://www.youtube.com/@Noctra11", StringComparison.OrdinalIgnoreCase) ||
                     tag.Equals("https://www.youtube.com/@Noctra11_Yt", StringComparison.OrdinalIgnoreCase)))
                {
                    button.Tag = "https://www.youtube.com/@Noctra11_Yt";
                }
            }
        }));
    }
}
