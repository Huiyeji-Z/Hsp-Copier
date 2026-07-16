namespace HspCopier.Services.Windows;

using System;
using System.Threading.Tasks;
using System.Windows;
using HspCopier.Core.Animations;
using HspCopier.Core.Windows;
using Microsoft.Extensions.Logging;

/// <summary>
/// 窗口状态服务实现：状态机 + 边缘检测 + 动画编排。
/// </summary>
public sealed class WindowStateService : IWindowStateService
{
    private readonly IEdgeDetector _edgeDetector;
    private readonly IAnimationEngine _animationEngine;
    private readonly ILogger<WindowStateService> _logger;

    private WindowVisualState _current = WindowVisualState.Expanded;
    private PinState _pin = PinState.Unpinned;
    private Rect _location;
    private Rect _lastExpandedRect;
    private bool _isAtEdge;
    private bool _isAnimating;

    public WindowStateService(
        IEdgeDetector edgeDetector,
        IAnimationEngine animationEngine,
        ILogger<WindowStateService> logger)
    {
        _edgeDetector = edgeDetector;
        _animationEngine = animationEngine;
        _logger = logger;
    }

    public WindowVisualState Current => _current;

    public PinState Pin => _pin;

    public bool IsAtEdge => _isAtEdge;

    public Rect LastExpandedRect => _lastExpandedRect;

    public event EventHandler<WindowVisualState>? StateChanged;

    public event EventHandler<PinState>? PinChanged;

    public event EventHandler<WindowVisualState>? AnimationCompleted;

    public async Task ExpandAsync()
    {
        if (_current == WindowVisualState.Expanded) return;
        if (_pin == PinState.Pinned) return;
        if (_isAnimating) return;

        _isAnimating = true;
        _logger.LogDebug("Expanding from {From}", _current);

        // 动画开始前先切到 Expanded 状态，让 UI 层提前显示主窗（从球心膨胀）
        SetState(WindowVisualState.Expanded);

        try
        {
            await _animationEngine.PlayAsync(AnimationDirection.Expand);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expand animation failed");
        }
        finally
        {
            _isAnimating = false;
            AnimationCompleted?.Invoke(this, WindowVisualState.Expanded);
        }
    }

    public async Task CollapseToBallAsync()
    {
        if (_current == WindowVisualState.CollapsedBall) return;
        if (_pin == PinState.Pinned)
        {
            _logger.LogDebug("Pinned, skip collapse");
            return;
        }
        if (_isAnimating) return;

        // 收缩前保存当前窗口矩形（用于展开时恢复尺寸）
        _lastExpandedRect = _location;
        _logger.LogDebug("Collapsing from {From}, saved rect={Rect}", _current, _location);

        _isAnimating = true;

        // 动画开始前先切到 CollapsedBall 状态，让 UI 层提前显示悬浮球（让大窗口向球收缩）
        SetState(WindowVisualState.CollapsedBall);

        try
        {
            await _animationEngine.PlayAsync(AnimationDirection.Collapse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collapse animation failed");
        }
        finally
        {
            _isAnimating = false;
            AnimationCompleted?.Invoke(this, WindowVisualState.CollapsedBall);
        }
    }

    public void SetPin(PinState state)
    {
        if (_pin == state) return;
        _pin = state;
        _logger.LogInformation("Pin state -> {State}", state);
        PinChanged?.Invoke(this, state);

        // 取消固定时，若当前位于边缘，自动触发收缩
        if (state == PinState.Unpinned && _isAtEdge && _current == WindowVisualState.Expanded)
        {
            _ = CollapseToBallAsync();
        }
    }

    public void UpdateWindowLocation(Rect location)
    {
        _location = location;

        // 处于展开状态时，持续更新"最近展开矩形"
        if (_current == WindowVisualState.Expanded)
        {
            _lastExpandedRect = location;
        }

        var wasAtEdge = _isAtEdge;
        _isAtEdge = _edgeDetector.IsAtScreenEdge(location);

        if (wasAtEdge != _isAtEdge)
        {
            _logger.LogDebug("AtEdge -> {AtEdge}", _isAtEdge);
        }
    }

    /// <summary>
    /// 鼠标离开窗口时调用。位于边缘 + 未固定 → 收缩。
    /// </summary>
    public void OnMouseLeave()
    {
        if (_pin == PinState.Pinned) return;
        if (_current != WindowVisualState.Expanded) return;
        if (!_isAtEdge) return;
        if (_isAnimating) return;

        _ = CollapseToBallAsync();
    }

    /// <summary>
    /// 鼠标进入悬浮球时调用。展开。
    /// </summary>
    public void OnBallMouseEnter()
    {
        if (_current != WindowVisualState.CollapsedBall) return;
        if (_isAnimating) return;
        _ = ExpandAsync();
    }

    private void SetState(WindowVisualState state)
    {
        if (_current == state) return;
        _current = state;
        _logger.LogInformation("Window state -> {State}", state);
        StateChanged?.Invoke(this, state);
    }
}
