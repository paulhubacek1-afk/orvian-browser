using System.Windows;

namespace Orvian.Browser;

public partial class PasswordPrompt : Window
{
    public string Password => PasswordBox.Password;

    public PasswordPrompt()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = !string.IsNullOrEmpty(PasswordBox.Password);
    }
}