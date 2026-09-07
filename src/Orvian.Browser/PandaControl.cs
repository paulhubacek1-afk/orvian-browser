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

    private static readonly Brush Fur = new SolidColorBrush(Color.FromRgb(25, 29, 37));
    private static readonly Brush White = Brushes.White;
    private static readonly Brush Soft = new SolidColorBrush(Color.FromRgb(238, 244, 251));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(47, 107, 255));
    private static readonly Brush Pink = new SolidColorBrush(Color.FromRgb(255, 151, 178));

    public PandaControl()
    {
        Width = 126;
        Height = 126;
        IsHitTestVisible = false;
        RenderTransformOrigin = new Point(.5, .78);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(1, 1));
        transforms.Children.Add(new RotateTransform(0));
        transforms.Children.Add(new TranslateTransform(0, 0));
        RenderTransform = transforms;

        var canvas = new Canvas { Width = 126, Height = 126 };
        Children.Add(canvas);

        AddEllipse(canvas, 20, 106, 86, 12, new SolidColorBrush(Color.FromArgb(28, 20, 30, 45)));
        AddEllipse(canvas, 18, 12, 38, 38, Fur);
        AddEllipse(canvas, 70, 12, 38, 38, Fur);
        AddEllipse(canvas, 13, 27, 100, 88, Fur);
        AddEllipse(canvas, 23, 37, 80, 70, White);

        // Eye patches
        AddEllipse(canvas, 29, 51, 34, 43, Fur, -18);
        AddEllipse(canvas, 63, 51, 34, 43, Fur, 18);
        _leftEye = AddEllipse(canvas, 42, 62, 15, 18, White);
        _rightEye = AddEllipse(canvas, 69, 62, 15, 18, White);
        _leftPupil = AddEllipse(canvas, 47, 67, 7, 10, Fur);
        _rightPupil = AddEllipse(canvas, 74, 67, 7, 10, Fur);

        // Nose + smile
        AddEllipse(canvas, 57, 79, 12, 8, Fur);
        var mouth = new Path
        {
            Stroke = Fur,
            StrokeThickness = 2.2,
            Data = Geometry.Parse("M 54,88 Q 63,96 72,88"),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(mouth);

        AddEllipse(canvas, 28, 88, 13, 7, Pink, opacity: .52);
        AddEllipse(canvas, 85, 88, 13, 7, Pink, opacity: .52);

        // Small blue collar badge gives the mascot an actual Orvian identity.
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
        Canvas.SetLeft(badge, 51);
        Canvas.SetTop(badge, 96);
        canvas.Children.Add(badge);

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _blinkTimer.Tick += (_, _) => Blink();
        _blinkTimer.Start();

        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
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

    private void Blink()
    {
        var animation = new DoubleAnimation(1, .03, TimeSpan.FromMilliseconds(95))
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
        Play(_random.Next(5) switch
        {
            0 => PandaMood.Curious,
            1 => PandaMood.Happy,
            2 => PandaMood.Thinking,
            3 => PandaMood.Excited,
            _ => PandaMood.Idle
        });
    }

    public void StartIdle()
    {
        _active?.Stop(this);
        _active = new Storyboard();

        var bob = new DoubleAnimation(-2.5, 2.5, TimeSpan.FromMilliseconds(1100))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(bob, this);
        Storyboard.SetTargetProperty(bob, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bob);

        _active.Begin(this, true);
    }

    public void Play(PandaMood mood)
    {
        _active?.Stop(this);
        _active = new Storyboard();

        var duration = TimeSpan.FromMilliseconds(mood is PandaMood.Excited ? 700 : 460);
        var y = mood switch
        {
            PandaMood.Happy or PandaMood.Success => -18,
            PandaMood.Excited => -25,
            PandaMood.Update => -21,
            PandaMood.Curious => -8,
            PandaMood.Error => 2,
            PandaMood.Sleep => 4,
            _ => -7
        };

        var bounce = new DoubleAnimation(0, y, duration)
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(bounce, this);
        Storyboard.SetTargetProperty(bounce, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bounce);

        var scale = mood is PandaMood.Success or PandaMood.Happy or PandaMood.Excited ? 1.10 : .97;
        var sx = new DoubleAnimation(1, scale, duration) { AutoReverse = true };
        var sy = new DoubleAnimation(1, mood is PandaMood.Success or PandaMood.Happy or PandaMood.Excited ? .92 : 1.02, duration) { AutoReverse = true };
        Storyboard.SetTarget(sx, this);
        Storyboard.SetTarget(sy, this);
        Storyboard.SetTargetProperty(sx, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
        Storyboard.SetTargetProperty(sy, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
        _active.Children.Add(sx);
        _active.Children.Add(sy);

        var tilt = mood == PandaMood.Error ? 7 : mood == PandaMood.Curious ? 4 : 2.5;
        var rotate = new DoubleAnimation(-tilt, tilt, duration)
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(rotate, this);
        Storyboard.SetTargetProperty(rotate, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)"));
        _active.Children.Add(rotate);

        _active.Completed += (_, _) => StartIdle();
        _active.Begin(this, true);
    }

    public void StopAnimations()
    {
        _blinkTimer.Stop();
        _activityTimer.Stop();
        _active?.Stop(this);
        _active = null;
    }
}
