using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Orvian.Browser;

public partial class MainWindow
{
    private sealed class OrvianTab
    {
        public required WebView2 View { get; init; }
        public string Title { get; set; } = "Neuer Tab";
        public string Address { get; set; } = "orvian://newtab";
        public Border? Visual { get; set; }
    }

    private readonly List<OrvianTab> _managedTabs = [];
    private OrvianTab? _activeManagedTab;
    private readonly bool _tabManagerHook = RegisterTabManagerHook();
    private bool _tabManagerInitialized;
    private bool _tabManagerClosed;

    private bool RegisterTabManagerHook()
    {
        Loaded += TabManager_Loaded;
        Closed += TabManager_Closed;
        return true;
    }

    private void TabManager_Loaded(object sender, RoutedEventArgs e)
    {
        if (_tabManagerClosed) return;
        InputManager.Current.PreProcessInput -= TabManager_PreProcessInput;
        InputManager.Current.PreProcessInput += TabManager_PreProcessInput;
        _ = InitializeManagedTabsAsync();
    }

    private void TabManager_Closed(object? sender, EventArgs e)
    {
        _tabManagerClosed = true;
        InputManager.Current.PreProcessInput -= TabManager_PreProcessInput;
        foreach (var tab in _managedTabs)
        {
            try { tab.View.Dispose(); } catch { }
        }
        _managedTabs.Clear();
    }

    private async Task InitializeManagedTabsAsync()
    {
        for (var i = 0; i < 250 && !_tabManagerInitialized; i++)
        {
            if (_browserReady && BrowserView.CoreWebView2 != null)
            {
                _tabManagerInitialized = true;
                PrepareManagedTabBar();
                SetManagedOverlayZOrder();
                RegisterExistingBrowserTab();
                UpdateManagedTabBar();
                return;
            }
            await Task.Delay(100);
        }
    }

    private void PrepareManagedTabBar()
    {
        try
        {
            var plus = FindDescendant<Button>(Chrome, b => Equals(b.Content?.ToString(), "+"));
            var strip = plus is null ? null : FindParent<StackPanel>(plus);
            if (strip is null) return;

            strip.Children.Clear();
            var add = CreateNewTabButton();
            strip.Children.Add(add);
        }
        catch { }
    }

    private void SetManagedOverlayZOrder()
    {
        Panel.SetZIndex(BrowserView, 0);
        Panel.SetZIndex(LockOverlay, 80);
        Panel.SetZIndex(WelcomeOverlay, 90);
        Panel.SetZIndex(UpdateOverlay, 90);
    }

    private void RegisterExistingBrowserTab()
    {
        if (_managedTabs.Any(t => ReferenceEquals(t.View, BrowserView))) return;
        var tab = new OrvianTab { View = BrowserView, Title = "Neuer Tab", Address = NewTabAddress };
        _managedTabs.Add(tab);
        _activeManagedTab = tab;
        BrowserView.Visibility = Visibility.Visible;
        AttachManagedTab(tab, existingCoreEvents: true);
        UpdateManagedTabVisual(tab);
    }

    private Button CreateNewTabButton()
    {
        var button = new Button
        {
            Content = "+",
            Width = 40,
            Height = 34,
            Margin = new Thickness(6, 0, 0, 4),
            FontSize = 20,
            ToolTip = "Neuer Tab"
        };
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _ = OpenManagedTabAsync();
        };
        return button;
    }

    private void AttachManagedTab(OrvianTab tab, bool existingCoreEvents = false)
    {
        var core = tab.View.CoreWebView2;
        if (core is null) return;

        if (!existingCoreEvents)
        {
            core.WebResourceRequested += (_, e) =>
            {
                try
                {
                    if (_blocker.ShouldBlock(e.Request.Uri))
                        e.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked by Orvian", "Content-Type: text/plain");
                }
                catch { }
            };

            core.NavigationStarting += (_, e) => ManagedNavigationStarting(tab, e);
            core.NavigationCompleted += (_, e) => ManagedNavigationCompleted(tab, e);
            core.NewWindowRequested += (_, e) => ManagedNewWindowRequested(tab, e);
            core.WebMessageReceived += (_, e) => ManagedWebMessageReceived(tab, e);
            core.DownloadStarting += (_, e) => ManagedDownloadStarting(tab, e);
            core.PermissionRequested += (_, e) => ManagedPermissionRequested(tab, e);
        }
    }

    private void ManagedNavigationStarting(OrvianTab tab, CoreWebView2NavigationStartingEventArgs e)
    {
        tab.Address = e.Uri;
        if (ReferenceEquals(tab, _activeManagedTab))
        {
            _internalPage = false;
            PrivacyStats.Text = "Orvian lädt die Seite …";
            _panda?.Play(PandaMood.Loading);
            return;
        }
        tab.Title = "Wird geladen …";
        UpdateManagedTabVisual(tab);
    }

    private async void ManagedNavigationCompleted(OrvianTab tab, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            if (tab.View.CoreWebView2 != null)
            {
                tab.Address = tab.View.CoreWebView2.Source ?? tab.Address;
                tab.Title = string.IsNullOrWhiteSpace(tab.View.CoreWebView2.DocumentTitle)
                    ? "Neuer Tab"
                    : tab.View.CoreWebView2.DocumentTitle;
            }

            UpdateManagedTabVisual(tab);

            if (ReferenceEquals(tab, _activeManagedTab))
            {
                BrowserView = tab.View;
                await NavigationCompleted(tab.View, e);
            }

            if (e.IsSuccess && tab.View.CoreWebView2 != null &&
                string.Equals(tab.Address, NewTabAddress, StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(30);
                await tab.View.CoreWebView2.ExecuteScriptAsync(SeasonalNewTabScript);
            }
        }
        catch
        {
            if (ReferenceEquals(tab, _activeManagedTab)) _panda?.Play(PandaMood.Error);
        }
    }

    private void ManagedNewWindowRequested(OrvianTab tab, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(e.Uri)) tab.View.CoreWebView2?.Navigate(e.Uri);
    }

    private async void ManagedWebMessageReceived(OrvianTab tab, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!ReferenceEquals(tab, _activeManagedTab)) return;
        await WebMessageReceived(tab.View.CoreWebView2, e);
    }

    private void ManagedDownloadStarting(OrvianTab tab, CoreWebView2DownloadStartingEventArgs e)
    {
        if (ReferenceEquals(tab, _activeManagedTab))
            DownloadStarting(tab.View.CoreWebView2, e);
    }

    private async void ManagedPermissionRequested(OrvianTab tab, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (ReferenceEquals(tab, _activeManagedTab))
            PermissionRequested(tab.View.CoreWebView2, e);
        else
            e.State = CoreWebView2PermissionState.Deny;
        await Task.CompletedTask;
    }

    private async Task OpenManagedTabAsync()
    {
        if (!_tabManagerInitialized || _browserReady == false || BrowserView.CoreWebView2?.Environment is not { } environment)
        {
            OpenNewTabPage();
            return;
        }

        try
        {
            _panda?.Play(PandaMood.Happy);
            var view = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Hidden
            };
            Panel.SetZIndex(view, 0);
            Page.Children.Add(view);
            await view.EnsureCoreWebView2Async(environment);

            var tab = new OrvianTab { View = view };
            _managedTabs.Add(tab);
            AttachManagedTab(tab);
            CreateManagedTabVisual(tab);
            await SwitchManagedTabAsync(tab, animate: true);
            view.CoreWebView2.NavigateToString(NewTabHtml);
        }
        catch
        {
            _panda?.Play(PandaMood.Error);
        }
    }

    private async Task SwitchManagedTabAsync(OrvianTab tab, bool animate)
    {
        if (_activeManagedTab is not null && !ReferenceEquals(_activeManagedTab, tab))
            _activeManagedTab.View.Visibility = Visibility.Hidden;

        _activeManagedTab = tab;
        BrowserView = tab.View;
        tab.View.Visibility = Visibility.Visible;
        AddressBox.Text = tab.Address;
        TabTitle.Text = tab.Title;
        BackButton.IsEnabled = tab.View.CanGoBack;
        ForwardButton.IsEnabled = tab.View.CanGoForward;
        UpdateManagedTabBar();

        if (animate)
        {
            tab.View.Opacity = 0.15;
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            var animation = new System.Windows.Media.Animation.DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(170))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            tab.View.BeginAnimation(OpacityProperty, animation);
        }
    }

    private async Task CloseManagedTabAsync(OrvianTab tab)
    {
        if (_managedTabs.Count <= 1)
        {
            tab.View.CoreWebView2?.NavigateToString(NewTabHtml);
            await SwitchManagedTabAsync(tab, animate: true);
            return;
        }

        var index = _managedTabs.IndexOf(tab);
        var replacement = _managedTabs[Math.Max(0, index - 1)];
        if (ReferenceEquals(tab, _activeManagedTab))
            await SwitchManagedTabAsync(replacement, animate: true);

        _managedTabs.Remove(tab);
        try { Page.Children.Remove(tab.View); } catch { }
        try { tab.View.Dispose(); } catch { }
        UpdateManagedTabBar();
        _panda?.Play(PandaMood.Happy);
    }

    private void CreateManagedTabVisual(OrvianTab tab)
    {
        var strip = GetManagedTabStrip();
        if (strip is null) return;
        var addButton = strip.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Content?.ToString(), "+"));

        var border = new Border
        {
            Width = 210,
            Height = 41,
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 222, 235)),
            BorderThickness = new Thickness(1, 1, 1, 0),
            CornerRadius = new CornerRadius(14, 14, 0, 0),
            Padding = new Thickness(8, 0, 7, 0),
            Margin = new Thickness(0, 0, 4, 0),
            Tag = tab
        };
        border.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _ = SwitchManagedTabAsync(tab, animate: false);
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

        var logo = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(47, 107, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
        logo.Child = new TextBlock { Text = "O", Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(logo);

        var title = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(5, 0, 3, 0)
        };
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var close = new Button
        {
            Content = "×",
            Width = 27,
            Height = 27,
            Padding = new Thickness(0),
            ToolTip = "Tab schließen"
        };
        Grid.SetColumn(close, 2);
        close.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _ = CloseManagedTabAsync(tab);
        };
        grid.Children.Add(close);

        border.Child = grid;
        tab.Visual = border;
        UpdateManagedTabVisual(tab);

        if (addButton != null)
        {
            var index = strip.Children.IndexOf(addButton);
            strip.Children.Insert(Math.Max(0, index), border);
        }
        else strip.Children.Add(border);
    }

    private void UpdateManagedTabVisual(OrvianTab tab)
    {
        if (tab.Visual?.Child is not Grid grid) return;
        if (grid.Children.OfType<TextBlock>().FirstOrDefault() is { } title)
            title.Text = string.IsNullOrWhiteSpace(tab.Title) ? "Neuer Tab" : tab.Title;

        var active = ReferenceEquals(tab, _activeManagedTab);
        if (tab.Visual != null)
        {
            tab.Visual.Background = new SolidColorBrush(active ? Color.FromRgb(255, 255, 255) : Color.FromRgb(239, 244, 250));
            tab.Visual.BorderBrush = new SolidColorBrush(active ? Color.FromRgb(47, 107, 255) : Color.FromRgb(213, 222, 233));
        }
    }

    private void UpdateManagedTabBar()
    {
        foreach (var tab in _managedTabs) UpdateManagedTabVisual(tab);
        if (_activeManagedTab is not null)
        {
            TabTitle.Text = string.IsNullOrWhiteSpace(_activeManagedTab.Title) ? "Neuer Tab" : _activeManagedTab.Title;
            AddressBox.Text = _activeManagedTab.Address;
        }
    }

    private StackPanel? GetManagedTabStrip()
    {
        return FindDescendant<StackPanel>(Chrome, s => s.Children.OfType<Button>().Any(b => Equals(b.Content?.ToString(), "+")));
    }

    private void TabManager_PreProcessInput(object? sender, PreProcessInputEventArgs e)
    {
        if (_tabManagerClosed || e.StagingItem.Input is not KeyEventArgs key || key.IsRepeat) return;
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        switch (key.Key)
        {
            case Key.T:
                e.StagingItem.Input = key;
                key.Handled = true;
                _ = OpenManagedTabAsync();
                break;
            case Key.W:
                e.StagingItem.Input = key;
                key.Handled = true;
                if (_activeManagedTab != null) _ = CloseManagedTabAsync(_activeManagedTab);
                break;
            case Key.Tab:
                e.StagingItem.Input = key;
                key.Handled = true;
                if (_managedTabs.Count > 1)
                {
                    var index = _activeManagedTab == null ? 0 : _managedTabs.IndexOf(_activeManagedTab);
                    var delta = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? -1 : 1;
                    var next = (index + delta + _managedTabs.Count) % _managedTabs.Count;
                    _ = SwitchManagedTabAsync(_managedTabs[next], animate: false);
                }
                break;
        }
    }
}
