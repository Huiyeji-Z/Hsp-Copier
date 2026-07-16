namespace HspCopier.Animations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HspCopier.Core.Animations;
using HspCopier.Core.Settings;
using Microsoft.Extensions.Logging;

/// <summary>
/// 动画引擎实现。维护策略注册表，按设置选择策略。
/// </summary>
public sealed class AnimationEngine : IAnimationEngine
{
    private readonly Dictionary<string, ITransformAnimation> _strategies;
    private readonly ISettingsService _settings;
    private readonly ILogger<AnimationEngine> _logger;
    private string _currentKey;

    // 动画期间冻结状态切换的视图引用（由 App 在窗口初始化时注入）
    public FrameworkElement? ExpandedView { get; set; }

    public FrameworkElement? BallView { get; set; }

    public Rect ExpandedRect { get; set; }

    public Rect BallRect { get; set; }

    public AnimationEngine(
        IEnumerable<ITransformAnimation> strategies,
        ISettingsService settings,
        ILogger<AnimationEngine> logger)
    {
        _strategies = strategies.ToDictionary(s => s.Key, s => s);
        _settings = settings;
        _logger = logger;
        _currentKey = settings.Current.AnimationKey;

        if (!_strategies.ContainsKey(_currentKey))
        {
            _currentKey = AnimationKeys.ScaleFade;
        }
    }

    public string CurrentKey => _currentKey;

    public IReadOnlyList<ITransformAnimation> AvailableStrategies => _strategies.Values.ToList();

    public void SetStrategy(string key)
    {
        if (!_strategies.ContainsKey(key))
        {
            _logger.LogWarning("Unknown animation key: {Key}", key);
            return;
        }
        _currentKey = key;
        _logger.LogInformation("Animation strategy -> {Key}", key);
    }

    public async Task PlayAsync(AnimationDirection direction)
    {
        if (ExpandedView == null)
        {
            _logger.LogWarning("Animation ExpandedView not initialized");
            return;
        }

        if (!_strategies.TryGetValue(_currentKey, out var strategy))
        {
            strategy = _strategies[AnimationKeys.ScaleFade];
        }

        _logger.LogInformation("Playing animation {Key} ({Dir})", _currentKey, direction);

        var (from, to) = direction == AnimationDirection.Collapse
            ? (ExpandedRect, BallRect)
            : (BallRect, ExpandedRect);

        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ctx = new AnimationContext(ExpandedView, BallView, from, to, direction, cts.Token);

        try
        {
            await strategy.AnimateAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Animation failed, fallback to ScaleFade");
            await _strategies[AnimationKeys.ScaleFade].AnimateAsync(ctx);
        }
    }
}
