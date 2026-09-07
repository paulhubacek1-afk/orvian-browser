using System.Windows;

namespace Orvian.Browser;

public partial class PasswordVaultWindow : Window
{
    private readonly PasswordVault _vault;

    public PasswordVaultWindow(PasswordVault vault)
    {
        InitializeComponent();
        _vault = vault;
        Loaded += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        var entries = _vault.LoadAll().ToList();
        CredentialList.ItemsSource = entries;
        CountText.Text = entries.Count == 1 ? "1 Eintrag" : $"{entries.Count} Einträge";
    }

    private void CredentialList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CredentialList.SelectedItem is not SavedCredential credential) return;
        SiteBox.Text = credential.Site;
        UsernameBox.Text = credential.Username;
        PasswordBox.Password = credential.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vault.Save(new SavedCredential(SiteBox.Text.Trim(), UsernameBox.Text.Trim(), PasswordBox.Password));
            RefreshList();
            CredentialList.SelectedIndex = -1;
            MessageBox.Show("Login sicher im Tresor gespeichert.", "Orvian", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Der Login konnte nicht gespeichert werden.\n\n" + ex.Message, "Orvian", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CredentialList.SelectedItem is not SavedCredential credential)
        {
            MessageBox.Show("Bitte zuerst einen gespeicherten Login auswählen.", "Orvian", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _vault.Delete(credential);
        SiteBox.Clear();
        UsernameBox.Clear();
        PasswordBox.Clear();
        RefreshList();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
