using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Orvian.Browser;

public partial class MainWindow
{
    private Border? _updateCard;
    private StackPanel? _updateActions;
    private Viewbox? _pandaViewbox;

    private void InitializeResponsiveUiHooks()
    {
        Loaded += ResponsiveUi_Loaded;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void ResponsiveUi_Loaded(object? sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(ApplyResponsiveUi), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyResponsiveUi()
    {
        if (Page == null) return;

        if (_pandaViewbox == null && _panda != null && ReferenceEquals(_panda.Parent, PandaLayer))
        {
            var index = PandaLayer.Children.IndexOf(_panda);
            PandaLayer.Children.Remove(_panda);
            _pandaViewbox = new Viewbox
            {
                Child = _panda,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 16, 14),
                IsHitTestVisible = false
            };
            PandaLayer.Children.Insert(Math.Max(0, index), _pandaViewbox);
        }

        if (_pandaViewbox != null)
        {
            var compact = ActualWidth < 1150 || ActualHeight < 760;
            var tiny = ActualWidth < 900 || ActualHeight < 650;
            _pandaViewbox.Width = tiny ? 76 : compact ? 96 : 126;
            _pandaViewbox.Height = _pandaViewbox.Width;
            _pandaViewbox.Margin = tiny
                ? new Thickness(0, 0, 8, 8)
                : compact
                    ? new Thickness(0, 0, 12, 10)
                    : new Thickness(0, 0, 16, 14);
        }

        if (UpdateOverlay?.Child is Border card)
        {
            _updateCard = card;
            var compact = ActualWidth < 850;
            var tiny = ActualWidth < 620;
            var available = Math.Max(280, ActualWidth - (tiny ? 24 : 56));

            card.Width = double.NaN;
            card.MinWidth = 0;
            card.MaxWidth = Math.Min(660, available);
            card.Margin = new Thickness(tiny ? 12 : compact ? 20 : 28);
            card.Padding = new Thickness(tiny ? 20 : compact ? 24 : 34);
            card.CornerRadius = new CornerRadius(tiny ? 20 : 27);

            if (card.Child is StackPanel stack)
            {
                foreach (var child in stack.Children)
                {
                    if (child is TextBlock text && text.Name == "UpdateText")
                        text.MaxWidth = Math.Max(230, card.MaxWidth - card.Padding.Left - card.Padding.Right);
                }

                var actions = stack.Children.OfType<StackPanel>().LastOrDefault();
                if (actions != null)
                {
                    _updateActions = actions;
                    actions.HorizontalAlignment = tiny ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
                    actions.Orientation = tiny ? Orientation.Vertical : Orientation.Horizontal;

                    foreach (var child in actions.Children.OfType<Button>())
                    {
                        child.Width = double.NaN;
                        child.HorizontalAlignment = tiny ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
                        child.Margin = tiny ? new Thickness(0, 5, 0, 0) : new Thickness(0, 0, 8, 0);
                        child.MinHeight = 44;
                    }
                }
            }
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveUi();
}
