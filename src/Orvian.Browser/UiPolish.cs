using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Orvian.Browser;

public partial class MainWindow
{
    private bool _postUpdateShowing;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyle = System.Windows.WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        Chrome.PreviewMouseLeftButtonDown += ChromeTitleBar_MouseLeftButtonDown;
        Loaded += ImprovedUi_Loaded;
        ReplaceWelcomePanda();
    }

    private void ChromeTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) != null)
            return;

        if (e.ClickCount == 2)
        {
            Maximize_Click(this, new RoutedEventArgs());
            return;
        }

        try
        {
            if (WindowState == WindowState.Maximized)
            {
                var mouse = PointToScreen(e.GetPosition(this));
                WindowState = WindowState.Normal;
                Left = Math.Max(0, mouse.X - (Width * 0.5));
                Top = Math.Max(0, mouse.Y - 18);
            }
            DragMove();
        }
        catch { }
    }

    private void ImprovedUi_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_postUpdateShowing) return;
        _ = CheckForPostUpdateWelcomeAsync();
    }

    private async Task CheckForPostUpdateWelcomeAsync()
    {
        await Task.Delay(160);
        var marker = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Orvian",
            "post-update.txt");

        if (!File.Exists(marker)) return;

        string version;
        try
        {
            version = (await File.ReadAllTextAsync(marker)).Trim();
            File.Delete(marker);
        }
        catch
        {
            return;
        }

        _postUpdateShowing = true;
        _welcomeVisible = true;
        WelcomeOverlay.Visibility = Visibility.Collapsed;
        UpdateOverlay.Visibility = Visibility.Collapsed;
        ShowPostUpdateOverlay(string.IsNullOrWhiteSpace(version) ? _updateChecker.CurrentVersion.ToString() : version);
    }

    private void ShowPostUpdateOverlay(string version)
    {
        if (Content is not Grid root) return;

        var overlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(232, 244, 248, 253)),
            Opacity = 0
        };
        Panel.SetZIndex(overlay, 100);

        var card = new Border
        {
            Width = 820,
            Padding = new Thickness(42, 34, 42, 28),
            CornerRadius = new CornerRadius(30),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(214, 228, 242)),
            Background = new LinearGradientBrush(
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(241, 248, 255),
                new Point(0, 0),
                new Point(1, 1)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 34,
                ShadowDepth = 8,
                Opacity = 0.18
            }
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        card.Child = layout;

        var pandaHost = new Grid
        {
            Width = 190,
            Height = 190,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var panda = new PandaControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new ScaleTransform(1.28, 1.28),
            RenderTransformOrigin = new Point(0.5, 0.78)
        };
        pandaHost.Children.Add(panda);
        Grid.SetColumn(pandaHost, 0);
        layout.Children.Add(pandaHost);

        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(content, 1);
        layout.Children.Add(content);

        content.Children.Add(new TextBlock
        {
            Text = "ORVIAN HAT SICH AKTUALISIERT",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(65, 127, 220))
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Willkommen bei Orvian {version}!",
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 35, 50)),
            Margin = new Thickness(0, 5, 0, 4)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Dein Panda kennt jetzt ein paar neue Tricks. 🐼✨",
            FontSize = 17,
            Foreground = new SolidColorBrush(Color.FromRgb(76, 105, 137)),
            Margin = new Thickness(0, 0, 0, 18)
        });

        var featurePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        AddUpdateFeature(featurePanel, "✨", "Mehr Panda-Animationen", "Blinzeln, Blickbewegungen, Bounce und weiche Übergänge.");
        AddUpdateFeature(featurePanel, "⚡", "Modernere Oberfläche", "Eigene Fensterleiste ohne doppelte Windows-Schaltflächen.");
        AddUpdateFeature(featurePanel, "🧭", "Mehr Browser-Werkzeuge", "New Tab, Command Palette, Verlauf, Lesezeichen und Downloads.");
        AddUpdateFeature(featurePanel, "🛡", "Mehr Schutz", "Datenschutz-Center, Berechtigungen und Netzwerkfilter.");
        content.Children.Add(featurePanel);

        var footer = new DockPanel { LastChildFill = false };
        var buildText = new TextBlock
        {
            Text = "Update erfolgreich installiert",
            Foreground = new SolidColorBrush(Color.FromRgb(92, 111, 132)),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12.5
        };
        DockPanel.SetDock(buildText, Dock.Left);
        footer.Children.Add(buildText);

        var continueButton = new Button
        {
            Content = "Loslegen 🚀",
            Width = 145,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)FindResource("PrimaryButton")
        };
        continueButton.Click += (_, _) => ClosePostUpdateOverlay(root, overlay, panda);
        DockPanel.SetDock(continueButton, Dock.Right);
        footer.Children.Add(continueButton);
        content.Children.Add(footer);

        overlay.Children.Add(card);
        root.Children.Add(overlay);
        panda.Play(PandaMood.Success);

        var overlayFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        overlay.BeginAnimation(OpacityProperty, overlayFade);

        var cardTransform = new ScaleTransform(0.90, 0.90);
        card.RenderTransform = cardTransform;
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        var cardScaleX = new DoubleAnimation(0.90, 1, TimeSpan.FromMilliseconds(520))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 }
        };
        var cardScaleY = cardScaleX.Clone();
        cardTransform.BeginAnimation(ScaleTransform.ScaleXProperty, cardScaleX);
        cardTransform.BeginAnimation(ScaleTransform.ScaleYProperty, cardScaleY);
    }

    private static void AddUpdateFeature(Panel panel, string icon, string title, string description)
    {
        var row = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(135, 232, 242, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 235, 245)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Child = grid;

        grid.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 19,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        var stack = new StackPanel();
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 48, 69))
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(94, 112, 133)),
            Margin = new Thickness(0, 2, 0, 0)
        });

        panel.Children.Add(row);
    }

    private void ClosePostUpdateOverlay(Grid root, Grid overlay, PandaControl panda)
    {
        panda.Play(PandaMood.Happy);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            root.Children.Remove(overlay);
            _welcomeVisible = false;
            _postUpdateShowing = false;
            _panda?.StartIdle();
            OpenNewTabPage();
        };
        overlay.BeginAnimation(OpacityProperty, fade);
    }

    private void ReplaceWelcomePanda()
    {
        var canvas = FindDescendant<Canvas>(WelcomeOverlay);
        if (canvas == null) return;
        canvas.Children.Clear();

        var dark = new SolidColorBrush(Color.FromRgb(53, 60, 72));
        var soft = new SolidColorBrush(Color.FromRgb(232, 237, 243));
        var blush = new SolidColorBrush(Color.FromRgb(255, 183, 194));
        var blue = new SolidColorBrush(Color.FromRgb(65, 143, 255));

        AddEllipse(canvas, 42, 151, 105, 15, new SolidColorBrush(Color.FromArgb(35, 30, 45, 65)));
        AddEllipse(canvas, 35, 22, 50, 50, soft);
        AddEllipse(canvas, 105, 22, 50, 50, soft);
        AddEllipse(canvas, 16, 42, 138, 116, dark);
        AddEllipse(canvas, 29, 54, 112, 96, Brushes.White);
        AddEllipse(canvas, 45, 73, 38, 50, dark, -18);
        AddEllipse(canvas, 89, 73, 38, 50, dark, 18);
        var leftEye = AddEllipse(canvas, 57, 84, 20, 25, Brushes.White);
        var rightEye = AddEllipse(canvas, 97, 84, 20, 25, Brushes.White);
        AddEllipse(canvas, 63, 91, 8, 11, dark);
        AddEllipse(canvas, 103, 91, 8, 11, dark);
        AddEllipse(canvas, 72, 113, 26, 18, dark);
        var smile = new Path
        {
            Stroke = dark,
            StrokeThickness = 3.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = Geometry.Parse("M 72,124 Q 85,135 98,124")
        };
        canvas.Children.Add(smile);
        AddEllipse(canvas, 35, 119, 16, 8, blush, opacity: 0.5);
        AddEllipse(canvas, 119, 119, 16, 8, blush, opacity: 0.5);
        AddEllipse(canvas, 42, 145, 38, 15, Brushes.White);
        AddEllipse(canvas, 102, 145, 38, 15, Brushes.White);
        AddEllipse(canvas, 69, 141, 42, 13, blue);
    }

    private static Ellipse AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush fill, double angle = 0, double opacity = 1)
    {
        var shape = new Ellipse
        {
            Width = width,
            Height = height,
            Fill = fill,
            Opacity = opacity,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        if (Math.Abs(angle) > 0.01) shape.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        canvas.Children.Add(shape);
        return shape;
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root == null) return null;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var nested = FindDescendant<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
