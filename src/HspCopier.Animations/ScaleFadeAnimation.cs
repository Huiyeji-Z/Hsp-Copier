namespace HspCopier.Animations;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HspCopier.Core.Animations;

/// <summary>
/// 缩放渐变动画。纯 WPF Storyboard：
/// ScaleTransform 1→0.15 + 背景透明度 1→0 + TranslateTransform 移动到球心。
/// 优化：动画期间临时禁用 DropShadowEffect，降低合成开销；
/// 透明度只作用于背景 brush，避免子树重绘；结束立刻释放。
/// </summary>
public sealed class ScaleFadeAnimation : ITransformAnimation
{
    public string Key => AnimationKeys.ScaleFade;

    public string DisplayName => "缩放渐变";

    public Task AnimateAsync(AnimationContext ctx)
    {
        var tcs = new TaskCompletionSource<bool>();
        var ct = ctx.CancellationToken;
        ct.Register(() => tcs.TrySetCanceled());

        var view = ctx.ExpandedView;
        if (view == null)
        {
            tcs.TrySetResult(true);
            return tcs.Task;
        }

        view.Dispatcher.Invoke(() =>
        {
            // 准备 RenderTransform：Scale + Translate 复合
            var group = view.RenderTransform as TransformGroup;
            if (group == null || group.Children.Count != 2
                || group.Children[0] is not ScaleTransform
                || group.Children[1] is not TranslateTransform)
            {
                group = new TransformGroup();
                group.Children.Add(new ScaleTransform());
                group.Children.Add(new TranslateTransform());
                view.RenderTransform = group;
                view.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];

            // 关键：每次刷新 Scale 的中心，避免窗口尺寸变化后中心偏移
            var w = view.RenderSize.Width;
            var h = view.RenderSize.Height;
            scale.CenterX = w / 2;
            scale.CenterY = h / 2;

            // 计算位移：Collapse 时窗口从原位置移到球心；Expand 时窗口从球心移回原位置
            var fromCenterX = ctx.FromRect.X + ctx.FromRect.Width / 2;
            var fromCenterY = ctx.FromRect.Y + ctx.FromRect.Height / 2;
            var toCenterX = ctx.ToRect.X + ctx.ToRect.Width / 2;
            var toCenterY = ctx.ToRect.Y + ctx.ToRect.Height / 2;
            var dx = toCenterX - fromCenterX;
            var dy = toCenterY - fromCenterY;

            var isCollapse = ctx.Direction == AnimationDirection.Collapse;
            var (startX, endX, startY, endY, startScale, endScale, startOpa, endOpa) =
                isCollapse
                    ? (0.0, dx, 0.0, dy, 1.0, 0.15, 1.0, 0.0)
                    : (dx, 0.0, dy, 0.0, 0.15, 1.0, 0.0, 1.0);

            // === 性能优化：动画期间临时降低视觉质量，降低合成开销 ===
            var savedEffect = view.Effect;
            var savedEdgeMode = RenderOptions.GetEdgeMode(view);
            var savedBitmapScalingMode = RenderOptions.GetBitmapScalingMode(view);
            var savedCachingHint = RenderOptions.GetCachingHint(view);

            view.Effect = null;                    // 去掉 DropShadow，避免每帧高斯模糊
            RenderOptions.SetEdgeMode(view, EdgeMode.Aliased);
            RenderOptions.SetBitmapScalingMode(view, BitmapScalingMode.LowQuality);
            RenderOptions.SetCachingHint(view, CachingHint.Cache);

            // 找到背景 brush（MainWindow 里叫 RootBackgroundBrush）
            Brush? bgBrush = null;
            if (view is Border b && b.Background is SolidColorBrush sb)
            {
                bgBrush = sb;
            }

            var storyboard = new Storyboard();
            var duration = TimeSpan.FromMilliseconds(500);
            var opacityDuration = TimeSpan.FromMilliseconds(380);

            var easeScale = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var easeMove = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var scaleXAnim = new DoubleAnimation
            {
                From = startScale,
                To = endScale,
                Duration = duration,
                EasingFunction = easeScale,
            };
            Storyboard.SetTarget(scaleXAnim, view);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
            storyboard.Children.Add(scaleXAnim);

            var scaleYAnim = new DoubleAnimation
            {
                From = startScale,
                To = endScale,
                Duration = duration,
                EasingFunction = easeScale,
            };
            Storyboard.SetTarget(scaleYAnim, view);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
            storyboard.Children.Add(scaleYAnim);

            var translateXAnim = new DoubleAnimation
            {
                From = startX,
                To = endX,
                Duration = duration,
                EasingFunction = easeMove,
            };
            Storyboard.SetTarget(translateXAnim, view);
            Storyboard.SetTargetProperty(translateXAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.X)"));
            storyboard.Children.Add(translateXAnim);

            var translateYAnim = new DoubleAnimation
            {
                From = startY,
                To = endY,
                Duration = duration,
                EasingFunction = easeMove,
            };
            Storyboard.SetTarget(translateYAnim, view);
            Storyboard.SetTargetProperty(translateYAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));
            storyboard.Children.Add(translateYAnim);

            // 透明度：优先作用于背景 brush（子树不重绘）；否则 fallback 到元素
            var opacityAnim = new DoubleAnimation
            {
                From = startOpa,
                To = endOpa,
                Duration = opacityDuration,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            if (bgBrush != null)
            {
                Storyboard.SetTarget(opacityAnim, bgBrush);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            }
            else
            {
                Storyboard.SetTarget(opacityAnim, view);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            }
            storyboard.Children.Add(opacityAnim);

            // 球窗透明度同步：
            // Collapse 时球窗从 0 渐显到 1（与主窗收缩同步，最终球窗接替视觉）
            // Expand 时球窗从 1 渐隐到 0（与主窗膨胀同步，最终主窗接管）
            if (ctx.BallView is { } ballView)
            {
                var (ballStartOpa, ballEndOpa) = isCollapse ? (0.0, 1.0) : (1.0, 0.0);
                // 初始值立刻设定，避免首帧闪烁
                ballView.Opacity = ballStartOpa;

                var ballOpacityAnim = new DoubleAnimation
                {
                    From = ballStartOpa,
                    To = ballEndOpa,
                    Duration = opacityDuration,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                Storyboard.SetTarget(ballOpacityAnim, ballView);
                Storyboard.SetTargetProperty(ballOpacityAnim, new PropertyPath("Opacity"));
                storyboard.Children.Add(ballOpacityAnim);
            }

            storyboard.Completed += (_, _) =>
            {
                storyboard.Remove(view);
                view.Effect = savedEffect;
                RenderOptions.SetEdgeMode(view, savedEdgeMode);
                RenderOptions.SetBitmapScalingMode(view, savedBitmapScalingMode);
                RenderOptions.SetCachingHint(view, savedCachingHint);
                tcs.TrySetResult(true);
            };

            view.Visibility = Visibility.Visible;
            storyboard.Begin(view, handoffBehavior: HandoffBehavior.SnapshotAndReplace, isControllable: false);
        });

        return tcs.Task;
    }
}
