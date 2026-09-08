using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;

namespace Orvian.Browser;

public partial class UpdateWelcomeWindow : Window
{
    public UpdateWelcomeWindow(string version)
    {
        InitializeComponent();
        VersionText.Text = $"Orvian {version} ist bereit.";
        Loaded += UpdateWelcomeWindow_Loaded;
    }

    private void UpdateWelcomeWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        BeginStoryboard((Storyboard)FindResource("Intro"));
        PandaMascot.Play(PandaMood.Update);
    }

    private void Continue_Click(object sender, RoutedEventArgs e) => Close();

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/paulhubacek1-afk/orvian-browser/releases/latest")
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        PandaMascot?.StopAnimations();
        base.OnClosed(e);
    }
}
