using System.Windows;

namespace HspCopier.Core.Windows;

/// <summary>
/// 窗口状态服务契约。负责状态机切换、动画编排入口、固定状态管理。
/// </summary>
public interface IWindowStateService
{
    WindowVisualState Current { get; }

    PinState Pin { get; }

    event EventHandler<WindowVisualState>? StateChanged;

    event EventHandler<PinState>? PinChanged;

    /// <summary>动画完成后触发（用于 UI 层做收尾：隐藏原窗口等）。</summary>
    event EventHandler<WindowVisualState>? AnimationCompleted;

    /// <summary>当前窗口是否位于屏幕边缘。</summary>
    bool IsAtEdge { get; }

    /// <summary>收缩前最后一次记录的窗口矩形（展开时恢复用）。</summary>
    Rect LastExpandedRect { get; }

    /// <summary>展开为窗口主体。</summary>
    Task ExpandAsync();

    /// <summary>收缩为悬浮球。</summary>
    Task CollapseToBallAsync();

    /// <summary>设置固定状态。固定后强制不变形。</summary>
    void SetPin(PinState state);

    /// <summary>更新窗口位置（拖动后调用），用于边缘判定。</summary>
    void UpdateWindowLocation(Rect location);
}
