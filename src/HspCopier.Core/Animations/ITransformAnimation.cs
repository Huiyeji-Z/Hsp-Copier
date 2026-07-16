namespace HspCopier.Core.Animations;

using System.Windows;

/// <summary>
/// 变形动画方向。
/// </summary>
public enum AnimationDirection
{
    /// <summary>主窗口 → 悬浮球。</summary>
    Collapse,

    /// <summary>悬浮球 → 主窗口。</summary>
    Expand,
}

/// <summary>
/// 动画上下文：包含参与动画的视图与几何信息。
/// </summary>
public sealed record AnimationContext(
    FrameworkElement ExpandedView,
    FrameworkElement? BallView,
    Rect FromRect,
    Rect ToRect,
    AnimationDirection Direction,
    CancellationToken CancellationToken);

/// <summary>
/// 变形动画策略契约。
/// </summary>
public interface ITransformAnimation
{
    /// <summary>策略 key（用于配置匹配）。</summary>
    string Key { get; }

    /// <summary>用户可见名称。</summary>
    string DisplayName { get; }

    /// <summary>执行一次变形动画。</summary>
    Task AnimateAsync(AnimationContext ctx);
}

/// <summary>
/// 动画引擎契约。负责按用户设置选择策略并播放。
/// </summary>
public interface IAnimationEngine
{
    /// <summary>当前策略 key。</summary>
    string CurrentKey { get; }

    /// <summary>切换策略。</summary>
    void SetStrategy(string key);

    /// <summary>列出所有已注册策略。</summary>
    IReadOnlyList<ITransformAnimation> AvailableStrategies { get; }

    /// <summary>播放一次变形动画。</summary>
    Task PlayAsync(AnimationDirection direction);
}

/// <summary>
/// 内置动画 key 常量。
/// </summary>
public static class AnimationKeys
{
    public const string ScaleFade = "ScaleFade";
}
