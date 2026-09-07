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

    private void General_Click(object sender, RoutedEventArgs e) => Show("Allgemein", "Startseite, Suche und grundlegende Browseroptionen.", GeneralPanel);
    private void Appearance_Click(object sender, RoutedEventArgs e) => Show("Darstellung", "Helles Design, Akzent und flüssige Orvian-Animationen.", AppearancePanel);
    private void PrivacySettings_Click(object sender, RoutedEventArgs e) => Show("Datenschutz & Blocker", "Kontrolliere Werbung, Tracker und sichere Navigation.", PrivacyPanel);
    private void Security_Click(object sender, RoutedEventArgs e) => Show("Sicherheit", "Passwörter und Schutzfunktionen von Orvian.", SecurityPanel);
    private void Performance_Click(object sender, RoutedEventArgs e) => Show("Leistung", "Optimiere Speicher, Tabs und Hardwarebeschleunigung.", PerformancePanel);
    private void Help_Click(object sender, RoutedEventArgs e) => Show("Wie du den Browser nutzt", "Alle wichtigen Tastaturkürzel und Browseraktionen.", HelpPanel);
    private void About_Click(object sender, RoutedEventArgs e) => Show("Über Orvian", "Informationen zum Browserprojekt.", AboutPanel);
}