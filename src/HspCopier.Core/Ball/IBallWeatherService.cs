namespace HspCopier.Core.Ball;

/// <summary>
/// 悬浮球天气服务契约:周期性检测网络/定位/天气,通知 UI 更新动画。
/// </summary>
public interface IBallWeatherService
{
    /// <summary>当前天气状态。</summary>
    WeatherCondition Current { get; }

    /// <summary>状态变化通知(已在 UI 线程触发)。</summary>
    event EventHandler? Changed;

    /// <summary>启动 30s 周期检测,立即触发一次 Refresh。</summary>
    Task StartAsync();

    /// <summary>停止周期检测。</summary>
    Task StopAsync();

    /// <summary>手动触发一次 Refresh(不影响计时器)。</summary>
    Task RefreshAsync();
}
