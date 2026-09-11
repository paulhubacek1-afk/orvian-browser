using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Orvian.Browser
{
    /// <summary>
    /// Animated vector-panda mascot drawn entirely with WPF shapes.
    /// Add it to a Panel and call Show() / Hide() / PlayHappy() etc.
    /// </summary>
    public class PandaControl
    {
        // ── Canvas & root ──────────────────────────────────────────
        private readonly Canvas   _root;
        private readonly Panel    _host;

        // ── Shape references for animation ─────────────────────────
        private readonly Ellipse  _leftPupil;
        private readonly Ellipse  _rightPupil;
        private readonly Ellipse  _leftEyeWhite;
        private readonly Ellipse  _rightEyeWhite;
        private readonly Ellipse  _leftBlink;
        private readonly Ellipse  _rightBlink;
        private readonly Path     _mouth;
        private readonly Ellipse  _blushL;
        private readonly Ellipse  _blushR;

        // ── Speech bubble ──────────────────────────────────────────
        private readonly Border        _bubble;
        private readonly TextBlock     _bubbleText;

        // ── Timers ─────────────────────────────────────────────────
        private readonly DispatcherTimer _blinkTimer;
        private readonly DispatcherTimer _bounceTimer;
        private          bool            _blinking;

        // ── Transform ──────────────────────────────────────────────
        private readonly TranslateTransform _bounce = new();
        private          double             _bounceDir = 1;

        // ── Config ─────────────────────────────────────────────────
        private const double Scale   = 1.0;
        private const double W       = 110 * Scale;   // canvas logical width
        private const double H       = 120 * Scale;   // canvas logical height
        private const double Margin  = 16;

        // ══════════════════════════════════════════════════════════
        public PandaControl(Panel host)
        {
            _host = host;
            _root = new Canvas { Width = W, Height = H, IsHitTestVisible = false };

            BuildPanda();

            _bubble     = BuildBubble(out _bubbleText);
            _bubble.Visibility = Visibility.Collapsed;
            _host.Children.Add(_bubble);

            _root.RenderTransform = _bounce;
            _host.Children.Add(_root);

            _root.SizeChanged += (_, __) => Reposition();
            _host.SizeChanged += (_, __) => Reposition();

            // Blink every 3–5 s
            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.6) };
            _blinkTimer.Tick += OnBlinkTick;
            _blinkTimer.Start();

            // Soft idle bob
            _bounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            _bounceTimer.Tick += OnBounceTick;
            _bounceTimer.Start();

            _root.Visibility = Visibility.Collapsed;
        }

        // ── Public API ────────────────────────────────────────────

        public bool IsVisible => _root.Visibility == Visibility.Visible;

        public void Show()
        {
            _root.Visibility   = Visibility.Visible;
            _bubble.Visibility = Visibility.Collapsed;
            Reposition();
            FadeIn(_root, 0.38);
        }

        public void Hide()
        {
            FadeOut(_root, 0.28, () =>
            {
                _root.Visibility   = Visibility.Collapsed;
                _bubble.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>Panda bounces happily and blushes.</summary>
        public void PlayHappy()
        {
            SetBlush(true);
            SetMouth("happy");
            var sb = new Storyboard();
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(0),   Value = 0 });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(140),  Value = -12, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(300),  Value = 0,  EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn  } });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(440),  Value = -9, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(580),  Value = 0,  EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn  } });
            Storyboard.SetTarget(anim, _bounce);
            Storyboard.SetTargetProperty(anim, new PropertyPath(TranslateTransform.YProperty));
            sb.Children.Add(anim);
            sb.Completed += (_, __) => { SetBlush(false); SetMouth("normal"); };
            sb.Begin();
        }

        /// <summary>Panda tilts and shows thinking dots.</summary>
        public void PlayThinking()
        {
            SetMouth("think");
            Say("…");
        }

        /// <summary>Back to calm idle stance.</summary>
        public void PlayIdle()
        {
            SetMouth("normal");
            SetBlush(false);
            HideBubble();
        }

        public void StopAnimations()
        {
            _blinkTimer.Stop();
            _bounceTimer.Stop();
        }

        /// <summary>Display a speech bubble with a short message.</summary>
        public void Say(string text, double autoHideSeconds = 3.5)
        {
            _bubbleText.Text = text;
            Reposition();
            _bubble.Visibility = Visibility.Visible;
            FadeIn(_bubble, 0.25);

            if (autoHideSeconds > 0)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoHideSeconds) };
                t.Tick += (_, __) => { HideBubble(); t.Stop(); };
                t.Start();
            }
        }

        public void HideBubble() =>
            FadeOut(_bubble, 0.2, () => _bubble.Visibility = Visibility.Collapsed);

        // ── Drawing ───────────────────────────────────────────────

        private void BuildPanda()
        {
            double s = Scale;

            // ── Ears ─────────────────────────────────────────────
            AddEllipse(2*s,  0,    22*s, 22*s, "#E0E0E0");   // left ear outer
            AddEllipse(86*s, 0,    22*s, 22*s, "#E0E0E0");   // right ear outer
            AddEllipse(9*s,  6*s,  12*s, 12*s, "#C89090");   // left inner
            AddEllipse(93*s, 6*s,  12*s, 12*s, "#C89090");   // right inner

            // ── Face ─────────────────────────────────────────────
            AddEllipse(7*s, 18*s, 96*s, 84*s, "#F4F4F4");   // big white face

            // ── Eye patches ──────────────────────────────────────
            AddEllipse(12*s, 32*s, 32*s, 26*s, "#1A1A1A");   // left patch
            AddEllipse(66*s, 32*s, 32*s, 26*s, "#1A1A1A");   // right patch

            // ── Eye whites ───────────────────────────────────────
            _leftEyeWhite  = AddEllipse(19*s, 37*s, 15*s, 15*s, "#FFFFFF");
            _rightEyeWhite = AddEllipse(76*s, 37*s, 15*s, 15*s, "#FFFFFF");

            // ── Pupils ───────────────────────────────────────────
            _leftPupil  = AddEllipse(22*s, 40*s,  9*s, 9*s, "#111111");
            _rightPupil = AddEllipse(79*s, 40*s,  9*s, 9*s, "#111111");

            // Glints
            AddEllipse(24*s, 40.5*s, 3*s, 3*s, "#FFFFFF");
            AddEllipse(81*s, 40.5*s, 3*s, 3*s, "#FFFFFF");

            // ── Blink covers (hidden by default) ─────────────────
            _leftBlink  = AddEllipse(12*s, 32*s, 32*s, 26*s, "#1A1A1A"); _leftBlink.Opacity  = 0;
            _rightBlink = AddEllipse(66*s, 32*s, 32*s, 26*s, "#1A1A1A"); _rightBlink.Opacity = 0;

            // ── Nose ─────────────────────────────────────────────
            AddEllipse(46*s, 72*s, 18*s, 12*s, "#2E2E2E");

            // ── Blush circles ────────────────────────────────────
            _blushL = AddEllipse(8*s,  74*s, 22*s, 12*s, "#E89090"); _blushL.Opacity = 0;
            _blushR = AddEllipse(80*s, 74*s, 22*s, 12*s, "#E89090"); _blushR.Opacity = 0;

            // ── Mouth ─────────────────────────────────────────────
            _mouth = new Path
            {
                Stroke          = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
                StrokeThickness = 2.4 * s,
                StrokeLineCap   = PenLineCap.Round,
                Data            = ParseGeometry("M 44,88 Q 55,96 66,88"),   // normal smile
            };
            _root.Children.Add(_mouth);

            // ── Bamboo stalk (decorative) ─────────────────────────
            var bamboo = new Path
            {
                Stroke          = new SolidColorBrush(Color.FromArgb(0x55, 0x57, 0xC4, 0x7A)),
                StrokeThickness = 5 * s,
                StrokeLineCap   = PenLineCap.Round,
                Data            = ParseGeometry("M 104,110 Q 108,85 100,60 Q 112,38 106,10"),
            };
            _root.Children.Insert(0, bamboo);   // behind everything
        }

        private Ellipse AddEllipse(double x, double y, double w, double h, string hex)
        {
            var el = new Ellipse
            {
                Width  = w,
                Height = h,
                Fill   = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            };
            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);
            _root.Children.Add(el);
            return el;
        }

        private Border BuildBubble(out TextBlock tb)
        {
            tb = new TextBlock
            {
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 13,
                FontWeight  = FontWeights.SemiBold,
                Foreground  = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth    = 160,
            };
            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(12, 12, 2, 12),
                Padding         = new Thickness(12, 8, 12, 8),
                Child           = tb,
                Effect          = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius   = 14, ShadowDepth = 3, Opacity = 0.55,
                    Color        = Colors.Black, Direction = 270
                },
            };
            return border;
        }

        // ── Positioning ───────────────────────────────────────────

        private void Reposition()
        {
            if (_host == null) return;
            double ph = _host.ActualHeight, pw = _host.ActualWidth;
            if (ph < 1 || pw < 1) return;

            // Panda — bottom-right corner
            Canvas.SetLeft(_root, pw - W - Margin);
            Canvas.SetTop (_root, ph - H - Margin);

            // Bubble — above panda
            var bw = _bubble.ActualWidth > 0 ? _bubble.ActualWidth : 180;
            var bh = _bubble.ActualHeight > 0 ? _bubble.ActualHeight : 48;
            Canvas.SetLeft(_bubble, pw - W - Margin - bw + W * 0.4);
            Canvas.SetTop (_bubble, ph - H - Margin - bh - 8);
        }

        // ── Idle animations ───────────────────────────────────────

        private double _bobPhase;

        private void OnBounceTick(object sender, EventArgs e)
        {
            _bobPhase += 0.055;
            _bounce.Y  = Math.Sin(_bobPhase) * 2.2;
        }

        private void OnBlinkTick(object sender, EventArgs e)
        {
            if (_blinking) return;
            _blinking = true;

            var close = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(60));
            var open  = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(80));
            open.BeginTime = TimeSpan.FromMilliseconds(90);
            open.Completed += (_, __) =>
            {
                _blinking = false;
                _blinkTimer.Interval = TimeSpan.FromSeconds(3 + new Random().NextDouble() * 2.5);
            };

            _leftBlink.BeginAnimation(UIElement.OpacityProperty, close);
            _rightBlink.BeginAnimation(UIElement.OpacityProperty, close);
            var openL = open.Clone(); var openR = open.Clone();
            openR.Completed += (_, __) => { _blinking = false; };
            _leftBlink.BeginAnimation(UIElement.OpacityProperty, openL);
            _rightBlink.BeginAnimation(UIElement.OpacityProperty, openR);
        }

        // ── Mood helpers ──────────────────────────────────────────

        private void SetMouth(string mood)
        {
            double s = Scale;
            _mouth.Data = ParseGeometry(mood switch
            {
                "happy"  => "M 42,86 Q 55,98 68,86",
                "think"  => "M 47,89 Q 55,93 63,89",
                "sad"    => "M 44,94 Q 55,86 66,94",
                _        => "M 44,88 Q 55,96 66,88",
            });
        }

        private void SetBlush(bool on)
        {
            var target = on ? 0.52 : 0.0;
            _blushL.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(200)));
            _blushR.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(200)));
        }

        // ── Fade helpers ──────────────────────────────────────────

        private static void FadeIn(UIElement el, double seconds)
        {
            el.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(seconds)));
        }

        private static void FadeOut(UIElement el, double seconds, Action? onComplete = null)
        {
            var a = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(seconds));
            if (onComplete != null) a.Completed += (_, __) => onComplete();
            el.BeginAnimation(UIElement.OpacityProperty, a);
        }

        private static Geometry ParseGeometry(string data) =>
            Geometry.Parse(data);
    }
}
