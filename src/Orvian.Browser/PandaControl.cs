using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Orvian.Browser;

public enum PandaMood { Idle, Happy, Thinking, Loading, Error, Success, Privacy, Sleep, Update, Curious, Excited }

public sealed class PandaControl : Grid
{
    private readonly Ellipse _leftEye;
    private readonly Ellipse _rightEye;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _activityTimer;
    private Storyboard? _active;
    private readonly Random _random = new();

    public PandaControl()
    {
        Width = 138;
        Height = 138;
        IsHitTestVisible = false;
        RenderTransformOrigin = new Point(0.5, 0.75);

        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new RotateTransform(0));
        group.Children.Add(new TranslateTransform(0, 0));
        RenderTransform = group;

        var canvas = new Canvas { Width = 138, Height = 138 };
        Children.Add(canvas);
        AddEllipse(canvas, 23, 111, 92, 18, new SolidColorBrush(Color.FromArgb(35, 20, 30, 50)));
        AddEllipse(canvas, 40, 4, 40, 40, new SolidColorBrush(Color.FromRgb(30, 34, 42)));
        AddEllipse(canvas, 58, 0, 40, 40, new SolidColorBrush(Color.FromRgb(30, 34, 42)));
        AddEllipse(canvas, 12, 28, 114, 98, new SolidColorBrush(Color.FromRgb(30, 34, 42)));
        AddEllipse(canvas, 21, 37, 96, 81, Brushes.White);
        AddEllipse(canvas, 34, 50, 38, 52, new SolidColorBrush(Color.FromRgb(30, 34, 42)), -22);
        AddEllipse(canvas, 72, 50, 38, 52, new SolidColorBrush(Color.FromRgb(30, 34, 42)), 22);
        _leftEye = AddEllipse(canvas, 50, 67, 10, 15, Brushes.White);
        _rightEye = AddEllipse(canvas, 78, 67, 10, 15, Brushes.White);
        AddEllipse(canvas, 53, 72, 5, 8, new SolidColorBrush(Color.FromRgb(30, 34, 42)));
        AddEllipse(canvas, 81, 72, 5, 8, new SolidColorBrush(Color.FromRgb(30, 34, 42)));
        AddEllipse(canvas, 63, 86, 12, 9, new SolidColorBrush(Color.FromRgb(30, 34, 42)));
        AddEllipse(canvas, 29, 94, 12, 6, new SolidColorBrush(Color.FromRgb(255, 167, 183)), opacity: 0.6);
        AddEllipse(canvas, 97, 94, 12, 6, new SolidColorBrush(Color.FromRgb(255, 167, 183)), opacity: 0.6);

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.7) };
        _blinkTimer.Tick += (_, _) => Blink();
        _blinkTimer.Start();

        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _activityTimer.Tick += (_, _) => RandomMicroAction();
        _activityTimer.Start();
        StartIdle();
    }

    private static Ellipse AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush fill, double angle = 0, double opacity = 1)
    {
        var shape = new Ellipse { Width = width, Height = height, Fill = fill, Opacity = opacity, RenderTransformOrigin = new Point(0.5, 0.5) };
        if (Math.Abs(angle) > 0.01) shape.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(shape, left); Canvas.SetTop(shape, top); canvas.Children.Add(shape);
        return shape;
    }

    private void Blink()
    {
        var a = new DoubleAnimation(1, 0.05, TimeSpan.FromMilliseconds(90)) { AutoReverse = true };
        _leftEye.BeginAnimation(OpacityProperty, a);
        _rightEye.BeginAnimation(OpacityProperty, a.Clone());
    }

    private void RandomMicroAction()
    {
        Play(_random.Next(4) switch
        {
            0 => PandaMood.Curious,
            1 => PandaMood.Happy,
            2 => PandaMood.Thinking,
            _ => PandaMood.Idle
        });
    }

    public void StartIdle()
    {
        _active?.Stop(this);
        _active = new Storyboard();
        var bob = new DoubleAnimation(-3, 3, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        Storyboard.SetTarget(bob, this);
        Storyboard.SetTargetProperty(bob, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)"));
        _active.Children.Add(bob);
        var tilt = new DoubleAnimation(-1.5, 1.5, TimeSpan.FromMilliseconds(1400)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        Storyboard.SetTarget(tilt, this);
        Storyboard.SetTargetProperty(tilt, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)"));
        _active.Children.Add(tilt);
        _active.Begin(this, true);
    }

    public void Play(PandaMood mood)
    {
        _active?.Stop(this);
        _active = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(mood is PandaMood.Excited ? 650 : 420);
        var y = mood switch
        {
            PandaMood.Happy or PandaMood.Success or PandaMood.Excited => -22,
            PandaMood.Update => -28,
            PandaMood.Curious => -12,
            PandaMood.Error => 3,
            PandaMood.Sleep => 5,
            _ => -10
        };
        var bounce = new DoubleAnimation(0, y, duration) { AutoReverse = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(bounce, this); Storyboard.SetTargetProperty(bounce, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(TranslateTransform.Y)")); _active.Children.Add(bounce);
        var sx = new DoubleAnimation(1, mood is PandaMood.Success or PandaMood.Happy or PandaMood.Excited ? 1.12 : 0.96, duration) { AutoReverse = true };
        Storyboard.SetTarget(sx, this); Storyboard.SetTargetProperty(sx, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)")); _active.Children.Add(sx);
        var sy = new DoubleAnimation(1, mood is PandaMood.Success or PandaMood.Happy or PandaMood.Excited ? 0.9 : 1.04, duration) { AutoReverse = true };
        Storyboard.SetTarget(sy, this); Storyboard.SetTargetProperty(sy, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)")); _active.Children.Add(sy);
        var rotateFrom = mood == PandaMood.Error ? -7 : mood == PandaMood.Curious ? -4 : -3;
        var rotateTo = mood == PandaMood.Error ? 7 : mood == PandaMood.Curious ? 4 : 3;
        var rotate = new DoubleAnimation(rotateFrom, rotateTo, duration) { AutoReverse = true };
        Storyboard.SetTarget(rotate, this); Storyboard.SetTargetProperty(rotate, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)")); _active.Children.Add(rotate);
        _active.Completed += (_, _) => StartIdle();
        _active.Begin(this, true);
    }
}
