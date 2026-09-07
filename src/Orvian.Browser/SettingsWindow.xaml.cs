using System.Windows;

namespace Orvian.Browser;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    private void Show(string title, string subtitle, UIElement panel)
    {
        SectionTitle.Text = title;
        SectionSubtitle.Text = subtitle;
        GeneralPanel.Visibility = Visibility.Collapsed;
        AppearancePanel.Visibility = Visibility.Collapsed;
        PrivacyPanel.Visibility = Visibility.Collapsed;
        SecurityPanel.Visibility = Visibility.Collapsed;
        PerformancePanel.Visibility = Visibility.Collapsed;
        HelpPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    private void General_Click(object sender, RoutedEventArgs e) => Show("Allgemein", "Startseite und grundlegende Browseroptionen.", GeneralPanel);
    private void Appearance_Click(object sender, RoutedEventArgs e) => Show("Darstellung", "Design und Panda-Verhalten.", AppearancePanel);
    private void PrivacySettings_Click(object sender, RoutedEventArgs e) => Show("Datenschutz", "Werbung, Tracker und lokaler Schutz.", PrivacyPanel);
    private void Security_Click(object sender, RoutedEventArgs e) => Show("Sicherheit", "Passwort-Tresor und Sicherheitsfunktionen.", SecurityPanel);
    private void Performance_Click(object sender, RoutedEventArgs e) => Show("Leistung", "Optionen für flüssiges und ressourcenschonendes Surfen.", PerformancePanel);
    private void Help_Click(object sender, RoutedEventArgs e) => Show("Tastatur & Hilfe", "Die wichtigsten Orvian-Kürzel.", HelpPanel);
    private void About_Click(object sender, RoutedEventArgs e) => Show("Über Orvian", "Versions- und Projektinformationen.", AboutPanel);
    private void OpenVault_Click(object sender, RoutedEventArgs e) => new PasswordVaultWindow(new PasswordVault()) { Owner = this }.ShowDialog();
}
