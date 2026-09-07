using System.Windows;

namespace Orvian.Browser;

public partial class PasswordEntryWindow : Window
{
    public string Site => SiteBox.Text.Trim();
    public string Username => UsernameBox.Text.Trim();
    public string Password => PasswordBox.Password;

    public PasswordEntryWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        Loaded += (_, _) => SiteBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Site) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            MessageBox.Show("Bitte Website, Benutzername und Passwort ausfüllen.", "Orvian Passwort-Tresor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}