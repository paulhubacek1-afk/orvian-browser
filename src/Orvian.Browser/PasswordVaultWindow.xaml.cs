using System.Windows;
using System.Windows.Controls;

namespace Orvian.Browser;

public partial class PasswordVaultWindow : Window
{
    private readonly PasswordVault _vault = new();
    private List<SavedCredential> _items = [];

    public PasswordVaultWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        Loaded += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        try
        {
            _items = _vault.LoadAll().ToList();
            CredentialList.Items.Clear();
            foreach (var item in _items)
            {
                CredentialList.Items.Add(new ListBoxItem
                {
                    Content = $"{item.Site}    •    {item.Username}",
                    Tag = item,
                    Padding = new Thickness(16, 13, 16, 13),
                    FontSize = 14
                });
            }
            EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Orvian Passwort-Tresor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordEntryWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _vault.Save(new SavedCredential(dialog.Site, dialog.Username, dialog.Password));
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Orvian Passwort-Tresor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CredentialList.SelectedItem is not ListBoxItem row || row.Tag is not SavedCredential credential) return;
        if (MessageBox.Show($"Zugangsdaten für {credential.Site} wirklich löschen?", "Orvian Passwort-Tresor", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _vault.Delete(credential);
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Orvian Passwort-Tresor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}