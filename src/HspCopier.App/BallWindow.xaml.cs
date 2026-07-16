namespace HspCopier.App;

using System.Windows;
using System.Windows.Input;
using HspCopier.Core.Ball;
using HspCopier.Core.Settings;
using HspCopier.Core.Windows;
using HspCopier.Services.Windows;
using Microsoft.Extensions.Logging;

/// <summary>
/// 悬浮球窗口。鼠标移入 → 触发展开。
/// 球内内容由 <see cref="BallAnimationStyle"/> 决定:Minion 或 Weather。
/// </summary>
public partial class BallWindow : Window
{
    private readonly IWindowStateService _windowState;
    private readonly IBallWeatherService _weather;
    private readonly ISettingsService _settings;
    private readonly ILogger<BallWindow> _logger;
    private BallAnimationStyle _currentStyle = BallAnimationStyle.Minion;

    public BallWindow(
        IWindowStateService windowState,
        IBallWeatherService weather,
        ISettingsService settings,
        ILogger<BallWindow> logger)
    {
        _windowState = windowState;
        _weather = weather;
        _settings = settings;
        _logger = logger;
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _settings.Changed += OnSettingsChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBallStyle(_settings.Current.BallAnimationStyle);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Changed 由 SettingsService 在保存成功后触发(可能在非 UI 线程,因 SaveAsync 是 async)
        // 但 SettingsService.RaiseChanged 同步调用,通常仍在 UI 线程
        Dispatcher.Invoke(() => ApplyBallStyle(_settings.Current.BallAnimationStyle));
    }

    private void ApplyBallStyle(BallAnimationStyle style)
    {
        if (style == _currentStyle && BallHost.Content != null) return;

        // 离开旧风格:若是 Weather,停止定时器
        if (_currentStyle == BallAnimationStyle.Weather && style != BallAnimationStyle.Weather)
        {
            _ = _weather.StopAsync();
        }

        _currentStyle = style;

        FrameworkElement? content = style switch
        {
            BallAnimationStyle.Minion => new Views.Ball.MinionBallControl(),
            BallAnimationStyle.Weather => new Views.Ball.WeatherBallControl(_weather),
            _ => null,
        };
        BallHost.Content = content;

        // 进入新风格:若是 Weather,启动定时器
        if (style == BallAnimationStyle.Weather)
        {
            _ = _weather.StartAsync();
        }

        _logger.LogInformation("Ball style applied: {Style}", style);
    }

    private void BallWindow_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (_windowState is WindowStateService ws)
        {
            ws.OnBallMouseEnter();
        }
    }
}
