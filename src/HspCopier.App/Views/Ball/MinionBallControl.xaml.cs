namespace HspCopier.App.Views.Ball;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

/// <summary>
/// 小黄人风格悬浮球:
/// - 每 3-5 秒瞳孔随机看向上下左右其中一个方向(1.2s 平滑过渡)
/// - 每 5-10 秒嘴部表情切换(正常微笑 / 张嘴 O / 惊讶 / 闭嘴微笑)
/// - 10% 概率在表情切换时插入一次眨眼
/// </summary>
public partial class MinionBallControl : UserControl
{
    private static readonly Random Rng = new();
    private readonly DispatcherTimer _gazeTimer;
    private readonly DispatcherTimer _expressionTimer;

    // 4 种嘴 Path Data(都是"一条线/弧"风格,宽 12 高 6):
    // 1. 平嘴直线 2. 微笑(向上弧) 3. 皱眉(向下弧) 4. 小 O 形(惊讶)
    private static readonly Geometry[] MouthGeometries =
    {
        Geometry.Parse("M 0,3 L 12,3"),               // 平嘴
        Geometry.Parse("M 0,5 Q 6,0 12,5"),           // 微笑(向上弧)
        Geometry.Parse("M 0,1 Q 6,6 12,1"),           // 皱眉(向下弧)
        Geometry.Parse("M 4,3 A 2,2 0 1 0 8,3"),     // 小 O 形
    };

    private int _currentMouth = 0;

    public MinionBallControl()
    {
        InitializeComponent();

        // 随机间隔 3-5 秒触发瞳孔
        _gazeTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(3 + Rng.NextDouble() * 2),
        };
        _gazeTimer.Tick += (_, _) =>
        {
            MovePupils();
            _gazeTimer.Interval = TimeSpan.FromSeconds(3 + Rng.NextDouble() * 2);
        };

        // 随机间隔 5-10 秒切换表情
        _expressionTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(5 + Rng.NextDouble() * 5),
        };
        _expressionTimer.Tick += (_, _) =>
        {
            ChangeExpression();
            _expressionTimer.Interval = TimeSpan.FromSeconds(5 + Rng.NextDouble() * 5);
        };

        Loaded += (_, _) =>
        {
            _gazeTimer.Start();
            _expressionTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _gazeTimer.Stop();
            _expressionTimer.Stop();
        };
    }

    private void MovePupils()
    {
        // 选 4 方向之一,瞳孔整体偏移
        var dir = Rng.Next(4);
        var (dx, dy) = dir switch
        {
            0 => (0.0, -3.5),  // 上
            1 => (0.0, 3.5),   // 下
            2 => (-3.5, 0.0),  // 左
            3 => (3.5, 0.0),   // 右
            _ => (0.0, 0.0),
        };
        var dur = TimeSpan.FromMilliseconds(1200);
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        AnimateTranslate(LeftPupilTranslate, dx, dy, dur, ease);
        AnimateTranslate(RightPupilTranslate, dx, dy, dur, ease);
    }

    private void ChangeExpression()
    {
        var next = (_currentMouth + 1 + Rng.Next(MouthGeometries.Length - 1)) % MouthGeometries.Length;
        _currentMouth = next;
        Mouth.Data = MouthGeometries[next];

        // 10% 概率眨眼:左右眼 ScaleY 1→0.1→1,300ms
        if (Rng.NextDouble() < 0.10)
        {
            Blink();
        }
    }

    private void Blink()
    {
        var blink = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            KeyFrames =
            {
                new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new LinearDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))),
                new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))),
            },
        };
        LeftEyeScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
        RightEyeScale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
    }

    private static void AnimateTranslate(TranslateTransform t, double x, double y, TimeSpan dur, IEasingFunction ease)
    {
        var ax = new DoubleAnimation(t.X, x, dur) { EasingFunction = ease };
        var ay = new DoubleAnimation(t.Y, y, dur) { EasingFunction = ease };
        t.BeginAnimation(TranslateTransform.XProperty, ax);
        t.BeginAnimation(TranslateTransform.YProperty, ay);
    }
}
