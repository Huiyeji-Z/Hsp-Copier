namespace HspCopier.Core.Windows;

/// <summary>
/// 窗口视觉状态：展开 / 贴边 / 收缩为悬浮球。
/// </summary>
public enum WindowVisualState
{
    /// <summary>完全展开的主窗口。</summary>
    Expanded,

    /// <summary>位于屏幕边缘但鼠标仍在区域内（过渡态）。</summary>
    DockedToEdge,

    /// <summary>已收缩为悬浮球。</summary>
    CollapsedBall,
}

/// <summary>
/// 固定状态。固定后强制不变形。
/// </summary>
public enum PinState
{
    Pinned,
    Unpinned,
}
