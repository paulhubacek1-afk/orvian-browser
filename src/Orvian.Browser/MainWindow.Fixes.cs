using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow
{
    private DispatcherTimer? _uiFixTimer;
    private bool _uiFixInitialized;

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

        PreviewKeyDown += ChromiumLikeShortcuts;

        _uiFixTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiFixTimer.Tick += (_, _) => SyncTabStripAndButtons();
        _uiFixTimer.Start();

        SyncTabStripAndButtons();
    }

    private void SyncTabStripAndButtons()
    {
        if (!IsLoaded) return;

        // Keep the visual tab strip synchronized with the real WebView2 tabs.
        // This fixes the case where a newly created tab existed internally but
        // its header was not attached to TabStrip.
        foreach (var tab in _tabs.ToArray())
        {
            if (!TabStrip.Children.Contains(tab.HeaderButton))
                TabStrip.Children.Add(tab.HeaderButton);

            StyleCloseButton(tab.HeaderButton);
        }

        for (var i = TabStrip.Children.Count - 1; i >= 0; i--)
        {
            if (TabStrip.Children[i] is Button header &&
                !_tabs.Any(t => ReferenceEquals(t.HeaderButton, header)))
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
        close.FontSize = 17;
        close.FontWeight = FontWeights.Normal;
        close.Cursor = Cursors.Hand;
        close.ToolTip = "Tab schließen";

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.PaddingProperty, new Thickness(0));
        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        var text = new FrameworkElementFactory(typeof(ContentPresenter));
        text.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        text.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(text);
        template.VisualTree = border;
        close.Template = template;
    }

    private void ChromiumLikeShortcuts(object sender, KeyEventArgs e)
    {
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
            var index = (int)e.Key - (int)Key.D1;
            SelectTabAt(index);
            e.Handled = true;
        }
        else if (e.Key == Key.D9)
        {
            SelectTabAt(_tabs.Count - 1);
            e.Handled = true;
        }
    }

    private void SelectAdjacentTab(int direction)
    {
        if (_tabs.Count < 2 || _activeTab == null) return;
        var index = _tabs.IndexOf(_activeTab);
        if (index < 0) return;
        index = (index + direction + _tabs.Count) % _tabs.Count;
        SelectTab(_tabs[index]);
    }

    private void SelectTabAt(int index)
    {
        if (index >= 0 && index < _tabs.Count)
            SelectTab(_tabs[index]);
    }
}
