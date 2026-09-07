using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow
{
    private DispatcherTimer? _tabSafetyTimer;
    private bool _uiFixInitialized;
    private bool _tabSyncQueued;
    private bool _manualUpdateCheckRunning;

    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnMainWindowClassLoaded),
            true);
    }

    private static void OnMainWindowClassLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window) return;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(window.InitializeUiFixes));
    }

    private void InitializeUiFixes()
    {
        if (_uiFixInitialized) return;
        _uiFixInitialized = true;

        ApplyChromiumChromeVisuals();
        PreviewKeyDown += ChromiumLikeShortcuts;
        PreviewMouseDown += OnMainWindowMouseDown;
        Closed += OnUiFixWindowClosed;

        // The old implementation woke the UI every 250 ms. Keep a small 2-second
        // safety net only for asynchronous WebView2 tab creation; normal actions
        // reconcile immediately after WPF input processing.
        _tabSafetyTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _tabSafetyTimer.Tick += (_, _) => ReconcileTabsIfNeeded();
        _tabSafetyTimer.Start();

        QueueTabReconcile();
    }

    private void OnUiFixWindowClosed(object? sender, EventArgs e)
    {
        _tabSafetyTimer?.Stop();
        _tabSafetyTimer = null;
        PreviewKeyDown -= ChromiumLikeShortcuts;
        PreviewMouseDown -= OnMainWindowMouseDown;
    }

    private void ApplyChromiumChromeVisuals()
    {
        var iconFont = new FontFamily("Segoe MDL2 Assets");

        BackButton.Content = "\uE72B";
        BackButton.FontFamily = iconFont;
        BackButton.FontSize = 16;
        BackButton.ToolTip = "Zurück (Alt+←)";

        ForwardButton.Content = "\uE72A";
        ForwardButton.FontFamily = iconFont;
        ForwardButton.FontSize = 16;
        ForwardButton.ToolTip = "Vorwärts (Alt+→)";

        NewTabButton.Content = "+";
        NewTabButton.FontFamily = new FontFamily("Segoe UI");
        NewTabButton.FontSize = 20;
        NewTabButton.FontWeight = FontWeights.Normal;

        foreach (var button in FindVisualChildren<Button>(this))
        {
            switch (button.ToolTip?.ToString())
            {
                case "Neu laden":
                    button.Content = "\uE72C";
                    button.FontFamily = iconFont;
                    button.FontSize = 15;
                    break;
                case "Startseite":
                    button.Content = "\uE80F";
                    button.FontFamily = iconFont;
                    button.FontSize = 15;
                    break;
                case "Lesezeichen":
                    button.Content = "\uE734";
                    button.FontFamily = iconFont;
                    button.FontSize = 15;
                    button.Background = Brushes.Transparent;
                    button.BorderBrush = Brushes.Transparent;
                    break;
                case "Datenschutz-Center":
                    button.Content = "\uE72E";
                    button.FontFamily = iconFont;
                    button.FontSize = 15;
                    break;
                case "Menü und Einstellungen":
                    button.Content = "\uE712";
                    button.FontFamily = iconFont;
                    button.FontSize = 16;
                    break;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) yield break;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private void OnMainWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var button = FindVisualParent<Button>(source);
        if (button == null) return;

        if (ReferenceEquals(button, NewTabButton) || IsTabButton(button))
            QueueTabReconcile();
    }

    private bool IsTabButton(Button button)
    {
        if (ReferenceEquals(button, NewTabButton)) return true;
        return _tabs.Any(tab => ReferenceEquals(tab.HeaderButton, button));
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void QueueTabReconcile()
    {
        if (_tabSyncQueued || !IsLoaded) return;
        _tabSyncQueued = true;

        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _tabSyncQueued = false;
            ReconcileTabsIfNeeded(forceVisualRefresh: true);
        }));
    }

    private void ReconcileTabsIfNeeded(bool forceVisualRefresh = false)
    {
        if (!IsLoaded) return;

        var missing = _tabs.Any(tab => !TabStrip.Children.Contains(tab.HeaderButton));
        var stale = TabStrip.Children.OfType<Button>().Any(header =>
            !_tabs.Any(tab => ReferenceEquals(tab.HeaderButton, header)));

        if (!missing && !stale && !forceVisualRefresh) return;

        foreach (var tab in _tabs)
        {
            if (!TabStrip.Children.Contains(tab.HeaderButton))
                TabStrip.Children.Add(tab.HeaderButton);

            StyleCloseButton(tab.HeaderButton);
        }

        for (var i = TabStrip.Children.Count - 1; i >= 0; i--)
        {
            if (TabStrip.Children[i] is Button header &&
                !_tabs.Any(tab => ReferenceEquals(tab.HeaderButton, header)))
            {
                TabStrip.Children.RemoveAt(i);
            }
        }

        UpdateTabVisuals();
    }

    private static void StyleCloseButton(Button header)
    {
        if (header.Content is not Grid grid) return;

        var close = grid.Children.OfType<Button>().FirstOrDefault();
        if (close == null || close.Tag as string == "orvian-close-styled") return;

        close.Tag = "orvian-close-styled";
        close.Background = Brushes.Transparent;
        close.BorderBrush = Brushes.Transparent;
        close.Foreground = new SolidColorBrush(Color.FromRgb(92, 104, 121));
        close.FontSize = 16;
        close.FontWeight = FontWeights.Normal;
        close.Cursor = Cursors.Hand;
        close.ToolTip = "Tab schließen";

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        border.SetValue(Border.PaddingProperty, new Thickness(0));
        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        template.VisualTree = border;
        close.Template = template;
    }

    private void ChromiumLikeShortcuts(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.U)
        {
            _ = ManualUpdateCheckAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Tab)
        {
            SelectAdjacentTab(-1);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Tab)
        {
            SelectAdjacentTab(1);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left)
        {
            Back_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right)
        {
            Forward_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        if (e.Key >= Key.D1 && e.Key <= Key.D8)
        {
            SelectTabAt((int)e.Key - (int)Key.D1);
            e.Handled = true;
        }
        else if (e.Key == Key.D9)
        {
            SelectTabAt(_tabs.Count - 1);
            e.Handled = true;
        }
    }

    private async Task ManualUpdateCheckAsync()
    {
        if (_manualUpdateCheckRunning || _locked) return;
        _manualUpdateCheckRunning = true;
        _panda?.Play(PandaMood.Thinking);

        try
        {
            var update = await _updateChecker.GetLatestAsync();
            if (update != null && update.Version > _updateChecker.CurrentVersion)
            {
                _pendingUpdate = update;
                if (!_welcomeVisible)
                    ShowPendingUpdate();
                return;
            }

            _panda?.Play(PandaMood.Success);
            MessageBox.Show(
                $"Orvian ist aktuell.\n\nInstalliert: v{_updateChecker.CurrentVersion}\nGeprüft gegen: GitHub Releases + VERSION.txt",
                "Orvian – Updates",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _panda?.Play(PandaMood.Error);
            MessageBox.Show(
                "Die Update-Prüfung konnte nicht abgeschlossen werden.\n\n" + ex.Message,
                "Orvian – Update-Prüfung",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _manualUpdateCheckRunning = false;
        }
    }

    private void SelectAdjacentTab(int direction)
    {
        if (_tabs.Count < 2 || _activeTab == null) return;
        var index = _tabs.IndexOf(_activeTab);
        if (index < 0) return;
        index = (index + direction + _tabs.Count) % _tabs.Count;
        SelectTab(_tabs[index]);
        QueueTabReconcile();
    }

    private void SelectTabAt(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            SelectTab(_tabs[index]);
            QueueTabReconcile();
        }
    }
}
