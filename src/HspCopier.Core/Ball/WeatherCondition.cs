namespace HspCopier.Core.Ball;

/// <summary>
/// 悬浮球天气动画展示状态。
/// </summary>
public enum WeatherCondition
{
    /// <summary>初始未知,尚未刷新过。</summary>
    Unknown,

    /// <summary>无网络连接。</summary>
    NoNetwork,

    /// <summary>有网但定位失败。</summary>
    NoLocation,

    /// <summary>晴天(白天)。</summary>
    Sunny,

    /// <summary>多云/阴天。</summary>
    Cloudy,

    /// <summary>下雨(含阵雨/毛毛雨)。</summary>
    Rainy,

    /// <summary>下雪(含阵雪)。</summary>
    Snowy,

    /// <summary>雷暴。</summary>
    Thunder,

    /// <summary>雾。</summary>
    Foggy,

    /// <summary>夜间晴(月亮 + 星星)。</summary>
    NightClear,
}
