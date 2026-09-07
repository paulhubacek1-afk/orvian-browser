using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class App : Application
{
    private static readonly string UpdateMarker = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orvian",
        "post-update.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ShowPostUpdateWelcomeIfNeeded));
    }

    private static void ShowPostUpdateWelcomeIfNeeded()
    {
        try
        {
            if (!File.Exists(UpdateMarker)) return;

            var version = File.ReadAllText(UpdateMarker).Trim();
            if (string.IsNullOrWhiteSpace(version)) version = "das neue Update";

            File.Delete(UpdateMarker);

            var updateWindow = new UpdateWelcomeWindow(version)
            {
                Owner = Current.MainWindow
            };
            updateWindow.ShowDialog();
        }
        catch
        {
            // A failed welcome animation must never prevent Orvian from starting.
        }
    }
}
