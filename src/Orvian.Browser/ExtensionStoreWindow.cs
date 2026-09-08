using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Orvian.Browser;

internal sealed class ExtensionStoreWindow : Window
{
    private readonly MainWindow _owner;
    private readonly ExtensionManager _manager;
    private readonly StackPanel _cards;
    private readonly TextBlock _status;

    internal ExtensionStoreWindow(MainWindow owner, ExtensionManager manager)
    {
        _owner = owner;
        _manager = manager;

        Title = "Orvian Extensions";
        Width = 860;
        Height = 650;
        MinWidth = 700;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = owner;
        Background = new SolidColorBrush(Color.FromRgb(243, 246, 250));
        Foreground = new SolidColorBrush(Color.FromRgb(23, 34, 54));

        _cards = new StackPanel();
        _status = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(103, 119, 142))
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _cards };
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(BuildHeader());
        Grid.SetRow(_status, 1);
        root.Children.Add(_status);
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);
        Content = root;

        Loaded += async (_, _) => await RefreshAsync();
    }

    private UIElement BuildHeader()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "🧩 Erweiterungen", FontSize = 32, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = "Orvian nutzt die Chromium-/WebView2-Extension-Architektur für installierbare Browser-Erweiterungen.", Margin = new Thickness(0, 6, 0, 0), FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(103, 119, 142)) });
        return stack;
    }

    private async Task RefreshAsync()
    {
        _cards.Children.Clear();
        var installed = await _manager.GetInstalledAsync(_owner.ExtensionProfile);
        foreach (var item in ExtensionManager.Catalog)
        {
            var extension = installed.FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            _cards.Children.Add(BuildCard(item, extension));
        }

        _status.Text = installed.Count == 0
            ? "Noch keine Erweiterungen installiert."
            : $"{installed.Count} Erweiterung{(installed.Count == 1 ? "" : "en")} installiert.";
    }

    private Border BuildCard(ExtensionManager.ExtensionCatalogItem item, CoreWebView2BrowserExtension? installed)
    {
        var card = new Border
        {
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(20),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(214, 224, 237)),
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(239, 244, 255)),
            Child = new TextBlock { Text = item.Id == ExtensionManager.SpotifyExtensionId ? "♫" : "🧩", FontSize = 25, Foreground = new SolidColorBrush(Color.FromRgb(47, 107, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        grid.Children.Add(icon);

        var text = new StackPanel();
        Grid.SetColumn(text, 1);
        text.Children.Add(new TextBlock { Text = item.Name, FontSize = 19, FontWeight = FontWeights.Bold });
        text.Children.Add(new TextBlock { Text = $"{item.Category}  •  v{item.Version}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(47, 107, 255)), Margin = new Thickness(0, 3, 0, 5) });
        text.Children.Add(new TextBlock { Text = item.Description, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(103, 119, 142)), MaxWidth = 560 });
        grid.Children.Add(text);

        var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(actions, 2);
        if (installed == null)
        {
            var install = ActionButton("Installieren", true);
            install.Click += async (_, _) =>
            {
                try
                {
                    if (_owner.ExtensionProfile == null) throw new InvalidOperationException("Die Browserengine ist noch nicht bereit.");
                    install.IsEnabled = false;
                    await _manager.InstallAsync(_owner.ExtensionProfile, item);
                    _owner.NotifyExtensionChanged();
                    await RefreshAsync();
                }
                catch (Exception ex)
                {
                    install.IsEnabled = true;
                    MessageBox.Show(ex.Message, "Orvian Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            actions.Children.Add(install);
        }
        else
        {
            var state = new TextBlock { Text = installed.IsEnabled ? "Aktiv" : "Deaktiviert", FontSize = 11, Foreground = installed.IsEnabled ? new SolidColorBrush(Color.FromRgb(38, 144, 98)) : new SolidColorBrush(Color.FromRgb(103, 119, 142)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,5) };
            actions.Children.Add(state);
            var toggle = ActionButton(installed.IsEnabled ? "Deaktivieren" : "Aktivieren", true);
            toggle.Click += async (_, _) =>
            {
                await _manager.SetEnabledAsync(installed, !installed.IsEnabled);
                await RefreshAsync();
                _owner.NotifyExtensionChanged();
            };
            actions.Children.Add(toggle);

            if (item.Id == ExtensionManager.SpotifyExtensionId)
            {
                var open = ActionButton("Mini-Player", false);
                open.Margin = new Thickness(0,7,0,0);
                open.Click += (_, _) => _owner.OpenSpotifyPanel();
                actions.Children.Add(open);
            }

            var remove = ActionButton("Entfernen", false);
            remove.Margin = new Thickness(0,7,0,0);
            remove.Click += async (_, _) =>
            {
                if (MessageBox.Show($"{item.Name} wirklich entfernen?", "Orvian Extensions", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                await _manager.RemoveAsync(installed);
                _owner.NotifyExtensionChanged();
                await RefreshAsync();
            };
            actions.Children.Add(remove);
        }
        grid.Children.Add(actions);
        card.Child = grid;
        return card;
    }

    private Button ActionButton(string text, bool primary)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 7, 14, 7),
            MinWidth = 120,
            Background = primary ? new SolidColorBrush(Color.FromRgb(47, 107, 255)) : Brushes.White,
            Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(43, 60, 85)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 221, 236)),
            BorderThickness = new Thickness(1)
        };
        return button;
    }
}
