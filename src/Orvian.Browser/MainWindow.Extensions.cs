using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Orvian.Browser;

public partial class MainWindow
{
    private readonly ExtensionManager _extensionManager = new();
    private SpotifyMiniPlayer? _spotifyMiniPlayer;
    private DispatcherTimer? _spotifyTimer;
    private Button? _extensionsButton;
    private bool _extensionsUiReady;

    private void InitializeExtensionUiHooks()
    {
        Loaded += ExtensionUi_Loaded;
        Closed += ExtensionUi_Closed;
        PreviewKeyDown += ExtensionUi_KeyDown;
    }

    private void ExtensionUi_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_extensionsUiReady) return;
        _extensionsUiReady = true;

        TryPrepareExtensionEnabledEnvironment();

        _extensionsButton = new Button
        {
            Content = "🧩",
            Width = 38,
            Height = 38,
            Margin = new Thickness(2),
            Foreground = (Brush)FindResource("AccentBrush"),
            ToolTip = "Erweiterungen (Ctrl+Shift+E)",
            Style = (Style)FindResource("RoundAction")
        };
        _extensionsButton.Click += (_, _) => OpenExtensionStore();
        if (PrivacyButton.Parent is StackPanel toolbar)
        {
            var index = toolbar.Children.IndexOf(PrivacyButton);
            toolbar.Children.Insert(Math.Max(0, index), _extensionsButton);
        }

        _spotifyMiniPlayer = new SpotifyMiniPlayer(this, Page);
        _spotifyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _spotifyTimer.Tick += async (_, _) => await RefreshSpotifyMiniPlayerAsync();
        _spotifyTimer.Start();
    }

    private void TryPrepareExtensionEnabledEnvironment()
    {
        if (_browserReady || _environment != null) return;

        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "WebView2");
            Directory.CreateDirectory(folder);
            var task = Task.Run(async () =>
            {
                var options = new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true };
                return await CoreWebView2Environment.CreateAsync(null, folder, options);
            });
            _environment = task.GetAwaiter().GetResult();
            _browserReady = true;
            _ = AddTabAsync(true, null);
        }
        catch
        {
            // MainWindow's normal initializer remains the fallback if the extensions-enabled environment cannot be created.
        }
    }

    private void ExtensionUi_Closed(object? sender, EventArgs e) => _spotifyTimer?.Stop();

    internal CoreWebView2Profile? ExtensionProfile => _activeTab?.View.CoreWebView2?.Profile;

    internal void OpenExtensionStore() => new ExtensionStoreWindow(this, _extensionManager).ShowDialog();

    internal void OpenSpotifyPanel()
    {
        if (_spotifyMiniPlayer == null) return;
        _spotifyMiniPlayer.SetVisible(true);
        if (_activeTab?.View.Source?.Host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase) != true)
            NavigateSpotifyHome();
    }

    internal void NotifyExtensionChanged() => _ = RefreshSpotifyMiniPlayerAsync();

    internal void NavigateSpotifyHome()
    {
        if (_activeTab != null) NavigateExternal(_activeTab, "https://open.spotify.com/");
    }

    internal Task OpenSpotifySearchAsync(string query)
    {
        var value = query.Trim();
        if (string.IsNullOrWhiteSpace(value) || _activeTab == null) return Task.CompletedTask;
        NavigateExternal(_activeTab, "https://open.spotify.com/search/" + Uri.EscapeDataString(value));
        return Task.CompletedTask;
    }

    internal Task ToggleSpotifyPlaybackAsync() => ControlSpotifyAsync("playpause");

    internal async Task ControlSpotifyAsync(string command)
    {
        var view = CurrentView();
        if (view?.CoreWebView2 == null) return;

        var script = command switch
        {
            "next" => "(() => { const root=document.querySelector('[data-testid=\"now-playing-widget\"]'); const b=[...(root?.querySelectorAll('button')||[])].find(x=>/next/i.test(x.getAttribute('aria-label')||'')); b?.click(); return true; })()",
            "previous" => "(() => { const root=document.querySelector('[data-testid=\"now-playing-widget\"]'); const b=[...(root?.querySelectorAll('button')||[])].find(x=>/previous/i.test(x.getAttribute('aria-label')||'')); b?.click(); return true; })()",
            _ => "(() => { const root=document.querySelector('[data-testid=\"now-playing-widget\"]'); const b=[...(root?.querySelectorAll('button')||[])].find(x=>/play|pause/i.test(x.getAttribute('aria-label')||'')); b?.click(); return true; })()"
        };

        try { await view.CoreWebView2.ExecuteScriptAsync(script); } catch { }
        await RefreshSpotifyMiniPlayerAsync();
    }

    private async Task RefreshSpotifyMiniPlayerAsync()
    {
        if (_spotifyMiniPlayer == null || _activeTab?.View.CoreWebView2 == null) return;

        var source = _activeTab.View.Source;
        var isSpotify = source?.Host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase) == true;
        if (!isSpotify)
        {
            _spotifyMiniPlayer.SetVisible(false);
            return;
        }

        try
        {
            var installed = await _extensionManager.FindByNameAsync(ExtensionProfile, "Spotify");
            if (installed == null || !installed.IsEnabled)
            {
                _spotifyMiniPlayer.SetVisible(false);
                return;
            }

            const string script = "(() => { const root=document.querySelector('[data-testid=\"now-playing-widget\"]'); if(!root) return {title:'',artist:'',playing:false}; const title=root.querySelector('[data-testid=\"context-item-link\"]')?.innerText || root.querySelector('a[href*=\"/track/\"]')?.innerText || ''; const artist=[...root.querySelectorAll('a[href*=\"/artist/\"]')].map(x=>x.innerText.trim()).filter(Boolean).join(', '); const p=[...root.querySelectorAll('button')].find(x=>/play|pause/i.test(x.getAttribute('aria-label')||'')); return {title,artist,playing:/pause/i.test(p?.getAttribute('aria-label')||'')}; })()";
            var json = await _activeTab.View.CoreWebView2.ExecuteScriptAsync(script);
            var state = JsonSerializer.Deserialize<SpotifyState>(json);
            if (state != null) _spotifyMiniPlayer.Update(state.Title, state.Artist, state.Playing);
            _spotifyMiniPlayer.SetVisible(true);
        }
        catch
        {
            _spotifyMiniPlayer.SetVisible(true);
            _spotifyMiniPlayer.Update("Spotify", "Mini-Player aktiv", false);
        }
    }

    private void ExtensionUi_KeyDown(object? sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.E)
        {
            OpenExtensionStore();
            e.Handled = true;
        }
    }

    private sealed record SpotifyState(string Title, string Artist, bool Playing)
    {
        public SpotifyState() : this(string.Empty, string.Empty, false) { }
    }
}
