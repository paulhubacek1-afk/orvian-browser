using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Orvian.Browser;

public partial class MainWindow
{
    private bool _postUpdateShowing;
    private bool _newTabPolishAttached;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyle = System.Windows.WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        Chrome.PreviewMouseLeftButtonDown += ChromeTitleBar_MouseLeftButtonDown;
        Loaded += ImprovedUi_Loaded;
        ReplaceWelcomePanda();
    }

    private void ChromeTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) != null)
            return;

        if (e.ClickCount == 2)
        {
            Maximize_Click(this, new RoutedEventArgs());
            return;
        }

        try
        {
            if (WindowState == WindowState.Maximized)
            {
                var mouse = PointToScreen(e.GetPosition(this));
                WindowState = WindowState.Normal;
                Left = Math.Max(0, mouse.X - (Width * 0.5));
                Top = Math.Max(0, mouse.Y - 18);
            }
            DragMove();
        }
        catch { }
    }

    private void ImprovedUi_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_postUpdateShowing) return;
        _ = CheckForPostUpdateWelcomeAsync();
        _ = AttachNewTabPolishAsync();
    }

    private async Task AttachNewTabPolishAsync()
    {
        if (_newTabPolishAttached) return;

        for (var i = 0; i < 150 && !_newTabPolishAttached; i++)
        {
            try
            {
                if (BrowserView.CoreWebView2 != null)
                {
                    BrowserView.NavigationCompleted += NewTabPolish_NavigationCompleted;
                    _newTabPolishAttached = true;
                    return;
                }
            }
            catch { }

            await Task.Delay(100);
        }
    }

    private async void NewTabPolish_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || BrowserView.CoreWebView2 == null) return;
        if (!string.Equals(BrowserView.Source, "orvian://newtab", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            await BrowserView.CoreWebView2.ExecuteScriptAsync(SeasonalNewTabScript);
        }
        catch { }
    }

    private const string SeasonalNewTabScript = """
(() => {
  const month = new Date().getMonth() + 1;
  const season = month === 12 || month <= 2 ? 'winter' : month <= 5 ? 'spring' : month <= 8 ? 'summer' : 'school';
  const title = season === 'school' ? 'Erster Schultag. Neuer Tab. 🎒' : 'Dein Browser. Dein Raum.';
  const subtitle = season === 'school' ? 'Der Orvian-Panda ist bereit für das neue Schuljahr.' : 'Privat, schnell und mit einem kleinen Panda als Begleitung.';
  const accessory = {
    school: `<g class="school"><rect x="136" y="122" width="52" height="58" rx="12" fill="#3567D8"/><rect x="143" y="130" width="38" height="38" rx="8" fill="#4C7EF0"/><path d="M147 127 Q162 111 177 127" fill="none" stroke="#244A9E" stroke-width="7" stroke-linecap="round"/><rect x="68" y="151" width="25" height="7" rx="3" fill="#F5B63F" transform="rotate(-18 68 151)"/><rect x="171" y="105" width="24" height="8" rx="3" fill="#F5B63F" transform="rotate(15 171 105)"/><path d="M174 112 l15 3 -12 6z" fill="#E05B5B"/></g>`,
    summer: `<g class="summer"><path d="M68 86 Q82 73 96 86" fill="none" stroke="#5B8CFF" stroke-width="7"/><path d="M124 86 Q138 73 152 86" fill="none" stroke="#5B8CFF" stroke-width="7"/><circle cx="79" cy="82" r="13" fill="#F7D56B" opacity=".9"/><circle cx="141" cy="82" r="13" fill="#F7D56B" opacity=".9"/><path d="M76 164 Q108 188 140 164" fill="none" stroke="#54B7E8" stroke-width="9" stroke-linecap="round"/></g>`,
    spring: `<g class="spring"><path d="M52 68 q-12 -18 5 -25 q13 9 1 25z" fill="#76C98B"/><circle cx="55" cy="44" r="8" fill="#F4A8C0"/><path d="M159 68 q12 -18 -5 -25 q-13 9 -1 25z" fill="#76C98B"/><circle cx="156" cy="44" r="8" fill="#F4A8C0"/><circle cx="48" cy="166" r="5" fill="#F4C85A"/><circle cx="168" cy="156" r="5" fill="#F4C85A"/></g>`,
    winter: `<g class="winter"><path d="M46 61 Q108 18 170 61 L164 76 Q108 49 52 76z" fill="#5B8CFF"/><circle cx="108" cy="36" r="7" fill="#FFFFFF"/><path d="M48 154 Q108 181 168 154" fill="none" stroke="#E8F3FF" stroke-width="13" stroke-linecap="round"/><g fill="#B9D8F5"><circle cx="27" cy="37" r="4"/><circle cx="183" cy="56" r="4"/><circle cx="20" cy="121" r="3"/><circle cx="190" cy="131" r="3"/></g></g>`
  }[season];

  document.documentElement.style.background = '#f7f9fc';
  document.body.innerHTML = `
    <style>
      *{box-sizing:border-box}html,body{margin:0;width:100%;min-height:100%;font-family:Segoe UI,Inter,Arial,sans-serif;color:#182235;background:#f7f9fc}
      body{overflow:auto}.page{max-width:1180px;margin:0 auto;padding:58px 42px 90px}.top{display:flex;align-items:center;gap:26px;margin-bottom:52px}.brand{display:flex;align-items:center;gap:12px;font-weight:800;letter-spacing:.08em;color:#2457e6}.logo{width:48px;height:48px;border-radius:16px;background:linear-gradient(135deg,#5b8cff,#2457e6);box-shadow:0 12px 28px #2457e62c;display:grid;place-items:center;position:relative;color:#fff;font-weight:900;font-size:22px}.logo:before,.logo:after{content:'';position:absolute;top:7px;width:11px;height:9px;border-radius:7px;background:#fff}.logo:before{left:8px}.logo:after{right:8px}.brand small{display:block;color:#7b8798;font-size:10px;letter-spacing:.12em;margin-top:2px}.hero{display:grid;grid-template-columns:300px 1fr;gap:58px;align-items:center}.mascot{height:320px;display:grid;place-items:center}.panda{width:270px;height:270px;animation:float 3.6s ease-in-out infinite;filter:drop-shadow(0 22px 25px #2c3e5c22)}.panda .body{fill:#333b49}.panda .face{fill:#fff}.panda .eye{fill:#333b49}.panda .shine{fill:#fff}.panda .cheek{fill:#ffb6c1;opacity:.65}.school,.summer,.spring,.winter{animation:accessory 2.8s ease-in-out infinite}.eyebrow{font-size:12px;font-weight:800;letter-spacing:.16em;color:#5c76a8;margin-bottom:12px}h1{font-size:54px;line-height:1.04;margin:0 0 14px;letter-spacing:-.04em}p{font-size:18px;line-height:1.55;color:#647087;margin:0 0 28px}.search{height:58px;display:flex;align-items:center;background:#fff;border:1px solid #ccd7e6;border-radius:29px;box-shadow:0 10px 28px #24324a12;padding:5px 7px 5px 20px;max-width:760px}.search input{flex:1;border:0;outline:0;font-size:16px;color:#182235;background:transparent}.search button{width:46px;height:46px;border:0;border-radius:23px;background:#2f6bff;color:#fff;font-size:20px;cursor:pointer}.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-top:38px}.card{border:1px solid #dde5ef;background:#fff;border-radius:18px;padding:19px;text-align:left;cursor:pointer;transition:transform .18s,box-shadow .18s,border-color .18s}.card:hover{transform:translateY(-3px);box-shadow:0 14px 30px #24324a18;border-color:#bfd0e7}.card b{display:block;font-size:14px;margin-bottom:6px}.card span{color:#718096;font-size:12.5px}.tip{margin-top:22px;color:#8a96a7;font-size:12px}@keyframes float{0%,100%{transform:translateY(0) rotate(-1deg)}50%{transform:translateY(-10px) rotate(1deg)}}@keyframes blink{0%,44%,48%,100%{transform:scaleY(1)}46%{transform:scaleY(.08)}}@keyframes accessory{0%,100%{transform:translateY(0)}50%{transform:translateY(-3px)}}.blink{transform-box:fill-box;transform-origin:center;animation:blink 4.5s infinite}
      @media(max-width:850px){.page{padding:35px 22px}.hero{grid-template-columns:1fr}.mascot{height:240px}.panda{width:210px;height:210px}h1{font-size:40px}.cards{grid-template-columns:1fr}}
    </style>
    <main class="page">
      <div class="top"><div class="brand"><div class="logo">O</div><div>ORVIAN<small>BROWSER</small></div></div></div>
      <section class="hero">
        <div class="mascot">
          <svg class="panda" viewBox="0 0 216 216" aria-label="Orvian Panda">
            <ellipse cx="108" cy="196" rx="62" ry="9" fill="#24324a" opacity=".10"/>
            <circle cx="61" cy="50" r="31" class="body"/><circle cx="155" cy="50" r="31" class="body"/>
            <circle cx="108" cy="111" r="77" class="body"/><ellipse cx="108" cy="119" rx="62" ry="66" class="face"/>
            <ellipse cx="78" cy="104" rx="27" ry="37" class="body" transform="rotate(-20 78 104)"/><ellipse cx="138" cy="104" rx="27" ry="37" class="body" transform="rotate(20 138 104)"/>
            <ellipse cx="81" cy="109" rx="13" ry="17" class="shine blink"/><ellipse cx="135" cy="109" rx="13" ry="17" class="shine blink"/>
            <circle cx="82" cy="113" r="6" class="eye"/><circle cx="134" cy="113" r="6" class="eye"/><circle cx="84" cy="111" r="2" class="shine"/><circle cx="136" cy="111" r="2" class="shine"/>
            <ellipse cx="108" cy="132" rx="11" ry="8" class="eye"/><path d="M95 143 Q108 155 121 143" fill="none" stroke="#333b49" stroke-width="4" stroke-linecap="round"/>
            <ellipse cx="59" cy="142" rx="10" ry="5" class="cheek"/><ellipse cx="157" cy="142" rx="10" ry="5" class="cheek"/>
            ${accessory}
          </svg>
        </div>
        <div><div class="eyebrow">ORVIAN BROWSER</div><h1>${title}</h1><p>${subtitle}</p>
          <form class="search" id="search"><input id="q" autocomplete="off" placeholder="Suchen oder Adresse eingeben …"/><button>⌕</button></form>
          <div class="cards"><button class="card" onclick="go('https://www.google.com/')"><b>🔎 Google</b><span>Web durchsuchen</span></button><button class="card" onclick="go('https://github.com/')"><b>◆ GitHub</b><span>Code &amp; Projekte</span></button><button class="card" onclick="go('https://www.youtube.com/')"><b>▶ YouTube</b><span>Videos</span></button><button class="card" onclick="msg('privacy')"><b>🛡 Datenschutz</b><span>Schutzstatus ansehen</span></button><button class="card" onclick="msg('history')"><b>🕘 Verlauf</b><span>Zuletzt besuchte Seiten</span></button><button class="card" onclick="msg('bookmarks')"><b>★ Lesezeichen</b><span>Gespeicherte Seiten</span></button></div>
          <div class="tip">Tipp: <b>Ctrl + K</b> öffnet die Command Palette.</div>
        </div>
      </section>
    </main>`;

  const w = window.chrome?.webview;
  window.msg = x => w?.postMessage(x);
  window.go = x => location.href = x;
  document.getElementById('search').onsubmit = e => { e.preventDefault(); const q = document.getElementById('q').value; if(q.trim()) w?.postMessage('search:' + q); };
})();
""";

    private async Task CheckForPostUpdateWelcomeAsync()
    {
        await Task.Delay(160);
        var marker = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Orvian",
            "post-update.txt");

        if (!File.Exists(marker)) return;

        string version;
        try
        {
            version = (await File.ReadAllTextAsync(marker)).Trim();
            File.Delete(marker);
        }
        catch { return; }

        _postUpdateShowing = true;
        _welcomeVisible = true;
        WelcomeOverlay.Visibility = Visibility.Collapsed;
        UpdateOverlay.Visibility = Visibility.Collapsed;
        ShowPostUpdateOverlay(string.IsNullOrWhiteSpace(version) ? _updateChecker.CurrentVersion.ToString() : version);
    }

    private void ShowPostUpdateOverlay(string version)
    {
        if (Content is not Grid root) return;

        var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(232, 244, 248, 253)), Opacity = 0 };
        Panel.SetZIndex(overlay, 100);
        var card = new Border
        {
            Width = 820, Padding = new Thickness(42, 34, 42, 28), CornerRadius = new CornerRadius(30), BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(214, 228, 242)),
            Background = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(241, 248, 255), new Point(0, 0), new Point(1, 1)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 34, ShadowDepth = 8, Opacity = 0.18 }
        };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        card.Child = layout;

        var pandaHost = new Grid { Width = 190, Height = 190, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var panda = new PandaControl { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new ScaleTransform(1.28, 1.28), RenderTransformOrigin = new Point(0.5, 0.78) };
        pandaHost.Children.Add(panda); Grid.SetColumn(pandaHost, 0); layout.Children.Add(pandaHost);

        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(content, 1); layout.Children.Add(content);
        content.Children.Add(new TextBlock { Text = "ORVIAN HAT SICH AKTUALISIERT", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(65, 127, 220)) });
        content.Children.Add(new TextBlock { Text = $"Willkommen bei Orvian {version}!", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(24, 35, 50)), Margin = new Thickness(0, 5, 0, 4) });
        content.Children.Add(new TextBlock { Text = "Dein Panda kennt jetzt ein paar neue Tricks. 🐼✨", FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(76, 105, 137)), Margin = new Thickness(0, 0, 0, 18) });
        var featurePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        AddUpdateFeature(featurePanel, "✨", "Mehr Panda-Animationen", "Blinzeln, Blickbewegungen, Bounce und weiche Übergänge.");
        AddUpdateFeature(featurePanel, "⚡", "Modernere Oberfläche", "Chrome-artige Tabs und eine einzige Fensterleiste ohne Doppelungen.");
        AddUpdateFeature(featurePanel, "🧭", "Mehr Browser-Werkzeuge", "New Tab, Command Palette, Verlauf, Lesezeichen und Downloads.");
        AddUpdateFeature(featurePanel, "🛡", "Mehr Schutz", "Datenschutz-Center, Berechtigungen und Netzwerkfilter.");
        content.Children.Add(featurePanel);
        var footer = new DockPanel { LastChildFill = false };
        var buildText = new TextBlock { Text = "Update erfolgreich installiert", Foreground = new SolidColorBrush(Color.FromRgb(92, 111, 132)), VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5 };
        DockPanel.SetDock(buildText, Dock.Left); footer.Children.Add(buildText);
        var continueButton = new Button { Content = "Loslegen 🚀", Width = 145, Height = 44, HorizontalAlignment = HorizontalAlignment.Right, Style = (Style)FindResource("PrimaryButton") };
        continueButton.Click += (_, _) => ClosePostUpdateOverlay(root, overlay, panda); DockPanel.SetDock(continueButton, Dock.Right); footer.Children.Add(continueButton); content.Children.Add(footer);
        overlay.Children.Add(card); root.Children.Add(overlay); panda.Play(PandaMood.Success);
        overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        var cardTransform = new ScaleTransform(0.90, 0.90); card.RenderTransform = cardTransform; card.RenderTransformOrigin = new Point(0.5, 0.5);
        var cardScaleX = new DoubleAnimation(0.90, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.18 } };
        cardTransform.BeginAnimation(ScaleTransform.ScaleXProperty, cardScaleX); cardTransform.BeginAnimation(ScaleTransform.ScaleYProperty, cardScaleX.Clone());
    }

    private static void AddUpdateFeature(Panel panel, string icon, string title, string description)
    {
        var row = new Border { CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Color.FromArgb(135, 232, 242, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(224, 235, 245)), BorderThickness = new Thickness(1), Padding = new Thickness(13, 10, 13, 10), Margin = new Thickness(0, 0, 0, 8) };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.Child = grid;
        grid.Children.Add(new TextBlock { Text = icon, FontSize = 19, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var stack = new StackPanel(); Grid.SetColumn(stack, 1); grid.Children.Add(stack);
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(31, 48, 69)) });
        stack.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromRgb(94, 112, 133)), Margin = new Thickness(0, 2, 0, 0) });
        panel.Children.Add(row);
    }

    private void ClosePostUpdateOverlay(Grid root, Grid overlay, PandaControl panda)
    {
        panda.Play(PandaMood.Happy);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        fade.Completed += (_, _) => { root.Children.Remove(overlay); _welcomeVisible = false; _postUpdateShowing = false; _panda?.StartIdle(); OpenNewTabPage(); };
        overlay.BeginAnimation(OpacityProperty, fade);
    }

    private void ReplaceWelcomePanda()
    {
        var canvas = FindDescendant<Canvas>(WelcomeOverlay);
        if (canvas == null) return;
        canvas.Children.Clear();
        var dark = new SolidColorBrush(Color.FromRgb(53, 60, 72));
        var soft = new SolidColorBrush(Color.FromRgb(232, 237, 243));
        var blush = new SolidColorBrush(Color.FromRgb(255, 183, 194));
        var blue = new SolidColorBrush(Color.FromRgb(65, 143, 255));
        AddEllipse(canvas, 42, 151, 105, 15, new SolidColorBrush(Color.FromArgb(35, 30, 45, 65)));
        AddEllipse(canvas, 35, 22, 50, 50, soft); AddEllipse(canvas, 105, 22, 50, 50, soft); AddEllipse(canvas, 16, 42, 138, 116, dark); AddEllipse(canvas, 29, 54, 112, 96, Brushes.White);
        AddEllipse(canvas, 45, 73, 38, 50, dark, -18); AddEllipse(canvas, 89, 73, 38, 50, dark, 18); AddEllipse(canvas, 57, 84, 20, 25, Brushes.White); AddEllipse(canvas, 97, 84, 20, 25, Brushes.White); AddEllipse(canvas, 63, 91, 8, 11, dark); AddEllipse(canvas, 103, 91, 8, 11, dark); AddEllipse(canvas, 72, 113, 26, 18, dark);
        var smile = new Path { Stroke = dark, StrokeThickness = 3.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Data = Geometry.Parse("M 72,124 Q 85,135 98,124") }; canvas.Children.Add(smile);
        AddEllipse(canvas, 35, 119, 16, 8, blush, opacity: 0.5); AddEllipse(canvas, 119, 119, 16, 8, blush, opacity: 0.5); AddEllipse(canvas, 42, 145, 38, 15, Brushes.White); AddEllipse(canvas, 102, 145, 38, 15, Brushes.White); AddEllipse(canvas, 69, 141, 42, 13, blue);
    }

    private static Ellipse AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush fill, double angle = 0, double opacity = 1)
    {
        var shape = new Ellipse { Width = width, Height = height, Fill = fill, Opacity = opacity, RenderTransformOrigin = new Point(0.5, 0.5) };
        if (Math.Abs(angle) > 0.01) shape.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(shape, left); Canvas.SetTop(shape, top); canvas.Children.Add(shape); return shape;
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root == null) return null;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T match) return match;
            var nested = FindDescendant<T>(child); if (nested != null) return nested;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null) { if (current is T match) return match; current = VisualTreeHelper.GetParent(current); }
        return null;
    }
}
