using System.Windows;

namespace Orvian.Browser;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }
}