using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Orvian.Browser;

public enum PandaMood
{
    Idle, Happy, Thinking, Loading, Error, Success, Privacy, Sleep, Update, Curious, Excited
}

public sealed class PandaControl : Grid
{
    private readonly Ellipse _leftEye;
    private readonly Ellipse _rightEye;
    private readonly Ellipse _leftPupil;
    private readonly Ellipse _rightPupil;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _activityTimer;
    private Storyboard? _active;
    private readonly Random _random = new();

    private static readonly Brush Black = new SolidColorBrush(Color.FromRgb(30, 34, 42));
    private static readonly Brush BlackSoft = new SolidColorBrush(Color.FromRgb(54, 59, 69));
    private static readonly Brush White = Brushes.White;
    private static readonly Brush Belly = new SolidColorBrush(Color.FromRgb(249, 251, 254));
    private static readonly Brush Soft = new SolidColorBrush(Color.FromRgb(236, 242, 249));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(47, 107, 255));
    private static readonly Brush Pink = new SolidColorBrush(Color.FromRgb(255, 145, 175));

    public PandaControl()
    {
        Width = 132;
        Height = 132;
        MinWidth = 72;
        MinHeight = 72;
        IsHitTestVisible = false;
        RenderTransformOrigin = new Point(.5, .76);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(1, 1));
        transforms.Children.Add(new RotateTransform(0));
        transforms.Children.Add(new TranslateTransform(0, 0));
        RenderTransform = transforms;

        var canvas = new Canvas { Width = 132, Height = 132 };
        Children.Add(canvas);

        // Friendly panda silhouette: round ears, white muzzle, eye patches, belly, paws.
        AddEllipse(canvas, 17, 116, 98, 10, new SolidColorBrush(Color.FromArgb(28, 25, 35, 50)));
        AddEllipse(canvas, 34, 68, 64, 56, Black);
        AddEllipse(canvas, 45, 78, 42, 34, Belly);
        AddEllipse(canvas, 35, 99, 27, 20, Black);
        AddEllipse(canvas, 70, 99, 27, 20, Black);
        AddEllipse(canvas, 39, 106, 16, 8, White, opacity: .92);
        AddEllipse(canvas, 77, 106, 16, 8, White, opacity: .92);
        AddEllipse(canvas, 24, 71, 25, 47, Black, -18);
        AddEllipse(canvas, 83, 71, 25, 47, Black, 18);
        AddEllipse(canvas, 25, 103, 17, 15, White, opacity: .94);
        AddEllipse(canvas, 90, 103, 17, 15, White, opacity: .94);

        AddEllipse(canvas, 17, 11, 39, 39, Black);
        AddEllipse(canvas, 76, 11, 39, 39, Black);
        AddEllipse(canvas, 26, 20, 21, 21, BlackSoft, opacity: .95);
        AddEllipse(canvas, 85, 20, 21, 21, BlackSoft, opacity: .95);
        AddEllipse(canvas, 13, 26, 106, 84, Black);
        AddEllipse(canvas, 23, 37, 86, 70, White);
        AddEllipse(canvas, 28, 45, 76, 61, Soft, opacity: .42);

        // Classic panda eye patches.
        AddEllipse(canvas, 28, 53, 33, 42, BlackSoft, -18);
        AddEllipse(canvas, 71, 53, 33, 42, BlackSoft, 18);
        _leftEye = AddEllipse(canvas, 38, 62, 16, 18, White);
        _rightEye = AddEllipse(canvas, 75, 62, 16, 18, White);
        _leftPupil = AddEllipse(canvas, 43, 66, 7, 10, Black);
        _rightPupil = AddEllipse(canvas, 80, 66, 7, 10, Black);
        AddEllipse(canvas, 45, 67, 2.8, 3.7, White);
        AddEllipse(canvas, 82, 67, 2.8, 3.7, White);

        AddRoundedBar(canvas, 39, 53, 13, 3, Black, -10);
        AddRoundedBar(canvas, 80, 53, 13, 3, Black, 10);

        // Small nose and smile instead of the previous hard-looking mouth.
        AddEllipse(canvas, 57, 82, 18, 11, Black);
        var mouth = new Path
        {
            Stroke = Black,
            StrokeThickness = 2.5,
            Data = Geometry.Parse("M 55,92 Q 66,103 77,92"),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(mouth);
        AddEllipse(canvas, 30, 86, 15, 8, Pink, opacity: .38);
        AddEllipse(canvas, 87, 86, 15, 8, Pink, opacity: .38);

        var badge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = Accent,
            BorderBrush = White,
            BorderThickness = new Thickness(2),
            Child = new TextBlock
            {
                Text = "O",
                Foreground = White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(badge, 54);
        Canvas.SetTop(badge, 99);
        canvas.Children.Add(badge);

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.4) };
        _blinkTimer.Tick += (_, _) => Blink();
        _blinkTimer.Start();

        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8.5) };
        _activityTimer.Tick += (_, _) => RandomMicroAction();
        _activityTimer.Start();

        StartIdle();
    }

    private static Ellipse AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush fill, double angle = 0, double opacity = 1)
    {
        var shape = new Ellipse
        {
            Width = width,
            Height = height,
            Fill = fill,
            Opacity = opacity,
            RenderTransformOrigin = new Point(.5, .5)
        };
        if (Math.Abs(angle) > .01)
            shape.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        canvas.Children.Add(shape);
        return shape;
    }

    private static void AddRoundedBar(Canvas canvas, double left, double top, double width, double height, Brush fill, double angle)
    {
        var bar = new Border
        {
            Width = width,
            Height = height,
            Background = fill,
            CornerRadius = new CornerRadius(2),
            RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = new RotateTransform(angle)
        };
        Canvas.SetLeft(bar, left);
        Canvas.SetTop(bar, top);
        canvas.Children.Add(bar);
    }

    private void Blink()
    {
        var animation = new DoubleAnimation(1, .04, TimeSpan.FromMilliseconds(105))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        _leftEye.BeginAnimation(OpacityProperty, animation);
        _rightEye.BeginAnimation(OpacityProperty, animation.Clone());
        _leftPupil.BeginAnimation(OpacityProperty, animation.Clone());
        _rightPupil.BeginAnimation(OpacityProperty, animation.Clone());
    }

    private void RandomMicroAction()
    {
        Play(_random.Next(6) switch
        {
            0 => PandaMood.Curious,
            1 => PandaMood.Happy,
            2 => PandaMood.Thinking,
            3 => PandaMood.Excited,
            4 => PandaMood.Success,
            _ => PandaMood.Idle
        });
    }

    public void StartIdle()
    {
        _active?.Stop(this);
        _active = new Storyboard();

        var bob = new DoubleAnimation(-2.0, 2.0, TimeSpan.FromMilliseconds(1250))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(bob, this);
        Storyboard.SetTargetProperty(bob, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bob);

        var breathe = new DoubleAnimation(1, 1.016, TimeSpan.FromMilliseconds(1500))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(breathe, this);
        Storyboard.SetTargetProperty(breathe, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
        _active.Children.Add(breathe);
        _active.Begin(this, true);
    }

    public void Play(PandaMood mood)
    {
        _active?.Stop(this);
        _active = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(mood == PandaMood.Excited ? 720 : 480);
        var y = mood switch
        {
            PandaMood.Happy or PandaMood.Success => -14,
            PandaMood.Excited => -22,
            PandaMood.Update => -17,
            PandaMood.Curious => -6,
            PandaMood.Error => 1,
            PandaMood.Sleep => 4,
            _ => -4
        };

        var bounce = new DoubleAnimation(0, y, duration)
        {
            AutoReverse = true,
            EasingFunction = new BackEase { Amplitude = .30, EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(bounce, this);
        Storyboard.SetTargetProperty(bounce, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bounce);

        var tilt = new DoubleAnimation(mood == PandaMood.Curious ? -7 : 0, mood == PandaMood.Curious ? 7 : 0, duration)
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(tilt, this);
        Storyboard.SetTargetProperty(tilt, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)"));
        _active.Children.Add(tilt);

        var scale = new DoubleAnimation(1, mood == PandaMood.Excited ? 1.06 : 1.03, duration)
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(scale, this);
        Storyboard.SetTargetProperty(scale, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
        _active.Children.Add(scale);

        _active.Completed += (_, _) => StartIdle();
        _active.Begin(this, true);
    }

    public void StopAnimations()
    {
        _active?.Stop(this);
        _blinkTimer.Stop();
        _activityTimer.Stop();
    }
}
