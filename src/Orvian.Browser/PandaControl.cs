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

    private static readonly Brush Fur = new SolidColorBrush(Color.FromRgb(24, 28, 36));
    private static readonly Brush FurSoft = new SolidColorBrush(Color.FromRgb(42, 48, 59));
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

        AddEllipse(canvas, 18, 108, 90, 11, new SolidColorBrush(Color.FromArgb(35, 20, 30, 45)));
        AddEllipse(canvas, 36, 78, 54, 42, Fur);
        AddEllipse(canvas, 43, 83, 40, 30, Soft);
        AddEllipse(canvas, 27, 99, 31, 20, Fur);
        AddEllipse(canvas, 68, 99, 31, 20, Fur);
        AddEllipse(canvas, 34, 104, 16, 7, FurSoft, opacity: .8);
        AddEllipse(canvas, 76, 104, 16, 7, FurSoft, opacity: .8);
        AddEllipse(canvas, 23, 76, 25, 39, Fur, -24);
        AddEllipse(canvas, 78, 76, 25, 39, Fur, 24);
        AddEllipse(canvas, 28, 101, 15, 15, White, opacity: .92);
        AddEllipse(canvas, 83, 101, 15, 15, White, opacity: .92);
        AddEllipse(canvas, 17, 10, 39, 39, Fur);
        AddEllipse(canvas, 70, 10, 39, 39, Fur);
        AddEllipse(canvas, 25, 18, 22, 22, FurSoft, opacity: .95);
        AddEllipse(canvas, 79, 18, 22, 22, FurSoft, opacity: .95);
        AddEllipse(canvas, 12, 25, 102, 91, Fur);
        AddEllipse(canvas, 21, 35, 84, 73, White);
        AddEllipse(canvas, 30, 75, 66, 31, Soft, opacity: .72);
        AddEllipse(canvas, 28, 49, 36, 45, Fur, -18);
        AddEllipse(canvas, 62, 49, 36, 45, Fur, 18);
        _leftEye = AddEllipse(canvas, 40, 61, 16, 19, White);
        _rightEye = AddEllipse(canvas, 70, 61, 16, 19, White);
        _leftPupil = AddEllipse(canvas, 45, 66, 7, 10, Fur);
        _rightPupil = AddEllipse(canvas, 75, 66, 7, 10, Fur);
        AddEllipse(canvas, 47, 67, 2.5, 3.5, White);
        AddEllipse(canvas, 77, 67, 2.5, 3.5, White);

        var leftBrow = new Border
        {
            Width = 14,
            Height = 3,
            Background = Fur,
            CornerRadius = new CornerRadius(2),
            RenderTransform = new RotateTransform(-12)
        };
        Canvas.SetLeft(leftBrow, 39);
        Canvas.SetTop(leftBrow, 54);
        canvas.Children.Add(leftBrow);

        var rightBrow = new Border
        {
            Width = 14,
            Height = 3,
            Background = Fur,
            CornerRadius = new CornerRadius(2),
            RenderTransform = new RotateTransform(12)
        };
        Canvas.SetLeft(rightBrow, 73);
        Canvas.SetTop(rightBrow, 54);
        canvas.Children.Add(rightBrow);

        AddEllipse(canvas, 57, 80, 12, 8, Fur);
        var mouth = new System.Windows.Shapes.Path
        {
            Stroke = Fur,
            StrokeThickness = 2.4,
            Data = Geometry.Parse("M 54,89 Q 63,98 72,89"),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(mouth);
        AddEllipse(canvas, 28, 87, 14, 8, Pink, opacity: .48);
        AddEllipse(canvas, 84, 87, 14, 8, Pink, opacity: .48);

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
        Canvas.SetTop(badge, 91);
        canvas.Children.Add(badge);

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
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

    private void Blink()
    {
        var duration = TimeSpan.FromMilliseconds(100);
        var animation = new DoubleAnimation(1, .04, duration)
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

        var bob = new DoubleAnimation(-2.2, 2.2, TimeSpan.FromMilliseconds(1250))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(bob, this);
        Storyboard.SetTargetProperty(bob, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bob);

        var breathe = new DoubleAnimation(1, 1.018, TimeSpan.FromMilliseconds(1500))
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
            PandaMood.Happy or PandaMood.Success => -17,
            PandaMood.Excited => -25,
            PandaMood.Update => -20,
            PandaMood.Curious => -7,
            PandaMood.Error => 2,
            PandaMood.Sleep => 4,
            _ => -5
        };

        var bounce = new DoubleAnimation(0, y, duration)
        {
            AutoReverse = true,
            EasingFunction = new BackEase { Amplitude = .35, EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(bounce, this);
        Storyboard.SetTargetProperty(bounce, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bounce);

        var tilt = new DoubleAnimation(mood == PandaMood.Curious ? -9 : 0, mood == PandaMood.Curious ? 9 : 0, duration)
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(tilt, this);
        Storyboard.SetTargetProperty(tilt, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)"));
        _active.Children.Add(tilt);

        var scale = new DoubleAnimation(1, mood == PandaMood.Excited ? 1.08 : 1.035, duration)
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
