using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Orvian.Browser;

public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(Button),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(StyleTabCloseButton));
    }

    private static void StyleTabCloseButton(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ToolTip is not string tooltip ||
            !tooltip.Equals("Tab schließen", StringComparison.Ordinal))
            return;

        button.Width = 30;
        button.Height = 30;
        button.Margin = new Thickness(3, 0, 0, 0);
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Background = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
        button.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol");
        button.FontSize = 15;
        button.FontWeight = FontWeights.Normal;
        button.Content = new TextBlock
        {
            Text = "×",
            Width = 24,
            Height = 24,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.Normal
        };
        button.Template = CreateTabCloseTemplate();
    }

    private static ControlTemplate CreateTabCloseTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var circle = new FrameworkElementFactory(typeof(Border));
        circle.Name = "Circle";
        circle.SetValue(Border.WidthProperty, 24.0);
        circle.SetValue(Border.HeightProperty, 24.0);
        circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        circle.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        circle.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        circle.AppendChild(presenter);

        template.VisualTree = circle;

        var hover = new Trigger
        {
            Property = Button.IsMouseOverProperty,
            Value = true
        };
        hover.Setters.Add(new Setter
        {
            TargetName = "Circle",
            Property = Border.BackgroundProperty,
            Value = new SolidColorBrush(Color.FromRgb(232, 237, 243))
        });
        template.Triggers.Add(hover);

        var pressed = new Trigger
        {
            Property = Button.IsPressedProperty,
            Value = true
        };
        pressed.Setters.Add(new Setter
        {
            TargetName = "Circle",
            Property = Border.BackgroundProperty,
            Value = new SolidColorBrush(Color.FromRgb(218, 225, 233))
        });
        template.Triggers.Add(pressed);

        return template;
    }
}
