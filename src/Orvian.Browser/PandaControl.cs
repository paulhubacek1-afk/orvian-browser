using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Orvian.Browser;

public enum PandaMood
{
    Idle, Happy, Thinking, Loading, Error, Success, Privacy, Sleep, Update
}

public sealed class PandaControl : Grid
{
    private readonly Ellipse _leftEye;
    private readonly Ellipse _rightEye;
    private readonly Ellipse _leftPupil;
    private readonly Ellipse _rightPupil;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _lookTimer;
    private Storyboard? _active;

    public PandaControl()
    {
        Width = 150;
        Height = 150;
        IsHitTestVisible = false;
        RenderTransformOrigin = new Point(0.5, 0.78);

        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new RotateTransform(0));
        group.Children.Add(new TranslateTransform(0, 0));
        RenderTransform = group;

        var canvas = new Canvas { Width = 150, Height = 150 };
        Children.Add(canvas);

        var dark = new SolidColorBrush(Color.FromRgb(53, 60, 72));
        var soft = new SolidColorBrush(Color.FromRgb(232, 237, 243));
        var blush = new SolidColorBrush(Color.FromRgb(255, 183, 194));
        var scarf = new SolidColorBrush(Color.FromRgb(65, 143, 255));

        AddEllipse(canvas, 28, 121, 94, 18, new SolidColorBrush(Color.FromArgb(38, 30, 45, 65)));
        AddEllipse(canvas, 34, 7, 43, 43, soft);
        AddEllipse(canvas, 73, 7, 43, 43, soft);
        AddEllipse(canvas, 10, 28, 130, 108, dark);
        AddEllipse(canvas, 20, 39, 110, 90, Brushes.White);

        AddEllipse(canvas, 34, 57, 40, 51, dark, -18);
        AddEllipse(canvas, 76, 57, 40, 51, dark, 18);

        _leftEye = AddEllipse(canvas, 49, 70, 19, 23, Brushes.White);
        _rightEye = AddEllipse(canvas, 82, 70, 19, 23, Brushes.White);
        _leftPupil = AddEllipse(canvas, 55, 77, 8, 11, dark);
        _rightPupil = AddEllipse(canvas, 88, 77, 8, 11, dark);

        AddEllipse(canvas, 64, 94, 22, 16, dark);
        var smile = new Path
        {
            Stroke = dark,
            StrokeThickness = 3.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = Geometry.Parse("M 65,105 Q 75,114 85,105")
        };
        canvas.Children.Add(smile);

        AddEllipse(canvas, 26, 100, 15, 8, blush, opacity: 0.55);
        AddEllipse(canvas, 109, 100, 15, 8, blush, opacity: 0.55);

        AddEllipse(canvas, 34, 120, 29, 18, Brushes.White);
        AddEllipse(canvas, 87, 120, 29, 18, Brushes.White);
        AddEllipse(canvas, 57, 118, 36, 12, scarf);

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.6) };
        _blinkTimer.Tick += (_, _) => Blink();
        _blinkTimer.Start();

        _lookTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.8) };
        _lookTimer.Tick += (_, _) => LookAround();
        _lookTimer.Start();

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
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        if (Math.Abs(angle) > 0.01) shape.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        canvas.Children.Add(shape);
        return shape;
    }

    private void Blink()
    {
        var a = new DoubleAnimation(1, 0.06, TimeSpan.FromMilliseconds(90))
        {
            AutoReverse = true
        };
        _leftEye.BeginAnimation(OpacityProperty, a);
        _rightEye.BeginAnimation(OpacityProperty, a.Clone());
        _leftPupil.BeginAnimation(OpacityProperty, a.Clone());
        _rightPupil.BeginAnimation(OpacityProperty, a.Clone());
    }

    private void LookAround()
    {
        var shift = (Random.Shared.NextDouble() - 0.5) * 5.0;
        AnimatePupil(_leftPupil, shift);
        AnimatePupil(_rightPupil, shift);
    }

    private static void AnimatePupil(Ellipse pupil, double x)
    {
        var transform = pupil.RenderTransform as TranslateTransform;
        if (transform == null)
        {
            transform = new TranslateTransform();
            pupil.RenderTransform = transform;
        }
        transform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(x, TimeSpan.FromMilliseconds(220))
            {
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    public void StartIdle()
    {
        _active?.Stop(this);
        _active = new Storyboard();

        AddAnimation("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)", -2.5, 2.5, 1000);
        AddAnimation("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)", 0.99, 1.02, 1200);
        AddAnimation("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)", 1.01, 0.985, 1200);
        AddAnimation("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)", -1.1, 1.1, 1500);

        _active.Begin(this, true);
    }

    private void AddAnimation(string property, double from, double to, int milliseconds)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        _active!.Children.Add(animation);
    }

    public void Play(PandaMood mood)
    {
        _active?.Stop(this);
        _active = new Storyboard();

        var bounce = mood switch
        {
            PandaMood.Happy or PandaMood.Success => -26,
            PandaMood.Update => -30,
            PandaMood.Error => 3,
            PandaMood.Sleep => 2,
            _ => -10
        };

        var duration = TimeSpan.FromMilliseconds(mood is PandaMood.Update ? 520 : 430);
        var jump = new DoubleAnimation(0, bounce, duration)
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(jump, this);
        Storyboard.SetTargetProperty(jump, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(jump);

        var squash = mood is PandaMood.Success or PandaMood.Happy ? 1.12 : 0.97;
        var stretch = mood is PandaMood.Success or PandaMood.Happy ? 0.90 : 1.03;
        var sx = new DoubleAnimation(1, squash, duration) { AutoReverse = true };
        var sy = new DoubleAnimation(1, stretch, duration) { AutoReverse = true };
        Storyboard.SetTarget(sx, this);
        Storyboard.SetTarget(sy, this);
        Storyboard.SetTargetProperty(sx, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
        Storyboard.SetTargetProperty(sy, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
        _active.Children.Add(sx);
        _active.Children.Add(sy);

        var fromAngle = mood == PandaMood.Error ? -4 : -2.2;
        var toAngle = mood == PandaMood.Error ? 4 : 2.2;
        var rotate = new DoubleAnimation(fromAngle, toAngle, duration) { AutoReverse = true };
        Storyboard.SetTarget(rotate, this);
        Storyboard.SetTargetProperty(rotate, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)"));
        _active.Children.Add(rotate);

        _active.Completed += (_, _) => StartIdle();
        _active.Begin(this, true);
    }
}
