namespace HspCopier.Services.Windows;

using System;
using System.Windows;
using HspCopier.Core.Windows;
using Microsoft.Extensions.Logging;

/// <summary>
/// 屏幕边缘检测器。仅检测左右两侧；窗口边缘接触或超出屏幕工作区即视为贴边。
/// </summary>
public sealed class EdgeDetector : IEdgeDetector
{
    private readonly ILogger<EdgeDetector> _logger;

    public EdgeDetector(ILogger<EdgeDetector> logger) => _logger = logger;

    public bool IsAtScreenEdge(Rect windowRect, double tolerance = 8)
    {
        return DetectEdge(windowRect, tolerance) != EdgeSide.None;
    }

    public EdgeSide DetectEdge(Rect windowRect, double tolerance = 8)
    {
        var workArea = SystemParameters.WorkArea;

        // 左边：窗口左侧接触或超出屏幕左侧
        if (windowRect.Left <= workArea.Left + tolerance)
        {
            _logger.LogDebug("Detected Left edge (Left={Left})", windowRect.Left);
            return EdgeSide.Left;
        }

        // 右边：窗口右侧接触或超出屏幕右侧
        if (windowRect.Right >= workArea.Right - tolerance)
        {
            _logger.LogDebug("Detected Right edge (Right={Right})", windowRect.Right);
            return EdgeSide.Right;
        }

        return EdgeSide.None;
    }
}
