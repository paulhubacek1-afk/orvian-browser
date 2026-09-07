using System.Windows;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow
{
    private readonly bool _updateWatchdogHook = RegisterUpdateWatchdogHook();
    private DispatcherTimer? _updateWatchdogTimer;
    private int _updateCheckRunning;

    private bool RegisterUpdateWatchdogHook()
    {
        Loaded += UpdateWatchdog_Loaded;
        Closed += UpdateWatchdog_Closed;
        return true;
    }

    private void UpdateWatchdog_Loaded(object sender, RoutedEventArgs e)
    {
        if (_updateWatchdogTimer != null) return;
        _updateWatchdogTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(30)
        };
        _updateWatchdogTimer.Tick += async (_, _) => await RunReliableUpdateCheckAsync();
        _updateWatchdogTimer.Start();
        _ = RunReliableUpdateCheckAsync();
    }

    private void UpdateWatchdog_Closed(object? sender, EventArgs e) => _updateWatchdogTimer?.Stop();

    private async Task RunReliableUpdateCheckAsync()
    {
        if (Interlocked.Exchange(ref _updateCheckRunning, 1) != 0) return;
        try
        {
            // Do not wait for WebView2, the welcome overlay, or the normal 6-hour UI timer.
            // GitHub can therefore be checked independently as soon as the app starts.
            await Task.Delay(TimeSpan.FromSeconds(3));
            var update = await _updateChecker.GetLatestAsync();
            if (update is null || update.Version <= _updateChecker.CurrentVersion) return;

            await Dispatcher.InvokeAsync(() =>
            {
                if (_pendingUpdate?.Version >= update.Version) return;
                _pendingUpdate = update;
                UpdateText.Text = $"Orvian {_updateChecker.CurrentVersion} ist installiert. Version {update.Version} ist auf GitHub verfügbar.";
                UpdateOverlay.Visibility = Visibility.Visible;
                BeginStoryboard((System.Windows.Media.Animation.Storyboard)FindResource("UpdateIntro"));
                _panda?.Play(PandaMood.Update);
            });
        }
        catch { }
        finally
        {
            Volatile.Write(ref _updateCheckRunning, 0);
        }
    }
}
