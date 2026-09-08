using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Orvian.Browser;

internal sealed class SpotifyMiniPlayer
{
    private readonly MainWindow _owner;
    private readonly Border _root;
    private readonly TextBlock _title;
    private readonly TextBlock _artist;
    private readonly TextBlock _status;
    private readonly Button _play;
    private readonly ListBox _queue;
    private readonly TextBox _queueInput;
    private readonly List<string> _items = new();

    internal SpotifyMiniPlayer(MainWindow owner, Grid host)
    {
        _owner = owner;

        _title = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(20, 31, 48)), TextTrimming = TextTrimming.CharacterEllipsis };
        _artist = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(105, 119, 140)), Margin = new Thickness(0, 3, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        _status = new TextBlock { Text = "Spotify", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(47, 107, 255)), FontWeight = FontWeights.Bold };
        _play = ActionButton("▶", async (_, _) => await _owner.ToggleSpotifyPlaybackAsync());
        var previous = ActionButton("◀◀", async (_, _) => await _owner.ControlSpotifyAsync("previous"));
        var next = ActionButton("▶▶", async (_, _) => await _owner.ControlSpotifyAsync("next"));
        var open = ActionButton("↗", (_, _) => _owner.NavigateSpotifyHome());

        _queue = new ListBox
        {
            Height = 94,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 9, 0, 8),
            FontSize = 12
        };
        _queue.SelectionChanged += async (_, _) =>
        {
            if (_queue.SelectedItem is string item)
            {
                _queue.SelectedItem = null;
                await _owner.OpenSpotifySearchAsync(item);
            }
        };

        _queueInput = new TextBox
        {
            Height = 32,
            Padding = new Thickness(10, 5, 10, 5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 222, 236)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White
        };
        _queueInput.KeyDown += (sender, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter) AddQueueItem();
        };

        var add = ActionButton("+", (_, _) => AddQueueItem());
        var clear = ActionButton("×", (_, _) => { _items.Clear(); RefreshQueue(); });

        var titleStack = new StackPanel();
        titleStack.Children.Add(_status);
        titleStack.Children.Add(_title);
        titleStack.Children.Add(_artist);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        controls.Children.Add(previous);
        controls.Children.Add(_play);
        controls.Children.Add(next);
        controls.Children.Add(open);

        var header = new Grid { Margin = new Thickness(0, 0, 0, 9) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(titleStack);
        Grid.SetColumn(controls, 1);
        header.Children.Add(controls);

        var queueHeader = new DockPanel();
        var queueLabel = new TextBlock { Text = "Meine Queue", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(68, 85, 108)) };
        queueHeader.Children.Add(queueLabel);
        DockPanel.SetDock(clear, Dock.Right);
        queueHeader.Children.Add(clear);

        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition());
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputGrid.Children.Add(_queueInput);
        Grid.SetColumn(add, 1);
        inputGrid.Children.Add(add);

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(queueHeader);
        body.Children.Add(_queue);
        body.Children.Add(inputGrid);

        _root = new Border
        {
            Width = 390,
            Padding = new Thickness(18),
            Margin = new Thickness(0, 18, 18, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(246, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 222, 237)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Effect = (System.Windows.Media.Effects.DropShadowEffect)Application.Current.FindResource("CardShadow"),
            Child = body,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_root, 80);
        host.Children.Add(_root);
    }

    internal void SetVisible(bool visible) => _root.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    internal void Update(string title, string artist, bool isPlaying)
    {
        _title.Text = string.IsNullOrWhiteSpace(title) ? "Keine Wiedergabe erkannt" : title;
        _artist.Text = string.IsNullOrWhiteSpace(artist) ? "Öffne Spotify und starte Musik." : artist;
        _play.Content = isPlaying ? "❚❚" : "▶";
    }

    private Button ActionButton(string text, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            Width = 34,
            Height = 32,
            Margin = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(0),
            FontSize = 12,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 222, 236)),
            BorderThickness = new Thickness(1),
            ToolTip = text
        };
        button.Click += click;
        return button;
    }

    private void AddQueueItem()
    {
        var item = _queueInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(item)) return;
        _items.Add(item);
        _queueInput.Clear();
        RefreshQueue();
    }

    private void RefreshQueue()
    {
        _queue.Items.Clear();
        foreach (var item in _items) _queue.Items.Add(item);
    }
}
