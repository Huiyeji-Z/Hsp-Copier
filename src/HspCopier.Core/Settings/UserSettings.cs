namespace HspCopier.Core.Settings;

/// <summary>
/// 窗口背景模式。
/// </summary>
public enum BackdropMode
{
    /// <summary>毛玻璃 Acrylic / Mica（Win11 优先）。</summary>
    Acrylic,

    /// <summary>纯色半透明。</summary>
    SolidColor,

    /// <summary>自定义背景图。</summary>
    Image,
}

/// <summary>
/// 用户设置（持久化到 config.json）。
/// </summary>
public sealed class UserSettings
{
    public BackdropMode Backdrop { get; set; } = BackdropMode.Acrylic;

    /// <summary>背景透明度 0.0~1.0。</summary>
    public double Opacity { get; set; } = 0.85;

    /// <summary>窗口背景图 key（内置 key 或自定义路径）。</summary>
    public string? WindowBackgroundKey { get; set; }

    /// <summary>悬浮球背景图/动图 key。</summary>
    public string? BallImageKey { get; set; } = "default-blink-face";

    /// <summary>悬浮球动态背景风格。默认卡通形象(小黄人),零联网。</summary>
    public BallAnimationStyle BallAnimationStyle { get; set; } = BallAnimationStyle.Minion;

    /// <summary>变形动画 key，4 选 1。</summary>
    public string AnimationKey { get; set; } = "ScaleFade";

    /// <summary>最大历史记录数。</summary>
    public int MaxHistoryItems { get; set; } = 10;

    /// <summary>主窗口左上角 X（屏幕坐标）。null 表示未保存过，由系统决定初始位置。</summary>
    public double? WindowLeft { get; set; }

    /// <summary>主窗口左上角 Y（屏幕坐标）。null 表示未保存过。</summary>
    public double? WindowTop { get; set; }

    /// <summary>主窗口宽度。null 表示用默认。</summary>
    public double? WindowWidth { get; set; }

    /// <summary>主窗口高度。null 表示用默认。</summary>
    public double? WindowHeight { get; set; }

    /// <summary>开机自启。</summary>
    public bool StartWithWindows { get; set; } = false;
}

/// <summary>
/// 设置服务契约。
/// </summary>
public interface ISettingsService
{
    UserSettings Current { get; }

    event EventHandler? Changed;

    Task LoadAsync();

    Task SaveAsync();

    Task UpdateAsync(Action<UserSettings> mutator);
}
