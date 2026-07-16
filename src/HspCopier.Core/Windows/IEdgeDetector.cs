using System.Windows;

namespace HspCopier.Core.Windows;

/// <summary>
/// 屏幕边缘检测器。判定窗口是否贴边（用于触发变形为悬浮球）。
/// </summary>
public interface IEdgeDetector
{
    /// <summary>判定窗口矩形是否贴在任意屏幕边缘。</summary>
    /// <param name="windowRect">窗口当前矩形。</param>
    /// <param name="tolerance">容差像素（默认 8）。</param>
    bool IsAtScreenEdge(Rect windowRect, double tolerance = 8);

    /// <summary>返回窗口贴边方向（None/Left/Right/Top/Bottom）。</summary>
    EdgeSide DetectEdge(Rect windowRect, double tolerance = 8);
}

public enum EdgeSide
{
    None,
    Left,
    Right,
    Top,
    Bottom,
}
