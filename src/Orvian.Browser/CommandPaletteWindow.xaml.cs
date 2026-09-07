using System.Windows;
using System.Windows.Input;

namespace Orvian.Browser;

public sealed record BrowserCommand(string Id, string Name, string Description, string Shortcut);

public partial class CommandPaletteWindow : Window
{
    private readonly List<BrowserCommand> _commands;
    public event EventHandler<string>? CommandInvoked;

    public CommandPaletteWindow(IEnumerable<BrowserCommand> commands)
    {
        InitializeComponent();
        _commands = commands.ToList();
        CommandList.ItemsSource = _commands;
        Loaded += (_, _) => { SearchBox.Focus(); CommandList.SelectedIndex = 0; };
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var q = SearchBox.Text.Trim();
        CommandList.ItemsSource = string.IsNullOrWhiteSpace(q)
            ? _commands
            : _commands.Where(c => (c.Name + " " + c.Description + " " + c.Id).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (CommandList.Items.Count > 0) CommandList.SelectedIndex = 0;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; return; }
        if (e.Key == Key.Enter) { InvokeSelected(); e.Handled = true; return; }
        if (e.Key == Key.Down) { Move(1); e.Handled = true; }
        else if (e.Key == Key.Up) { Move(-1); e.Handled = true; }
    }

    private void Move(int delta)
    {
        if (CommandList.Items.Count == 0) return;
        var next = CommandList.SelectedIndex + delta;
        if (next < 0) next = CommandList.Items.Count - 1;
        if (next >= CommandList.Items.Count) next = 0;
        CommandList.SelectedIndex = next;
        CommandList.ScrollIntoView(CommandList.SelectedItem);
    }

    private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => InvokeSelected();

    private void InvokeSelected()
    {
        if (CommandList.SelectedItem is not BrowserCommand cmd) return;
        CommandInvoked?.Invoke(this, cmd.Id);
        DialogResult = true;
    }
}
