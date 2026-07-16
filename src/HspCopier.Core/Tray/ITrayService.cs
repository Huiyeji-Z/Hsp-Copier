namespace HspCopier.Core.Tray;

/// <summary>
/// 系统托盘服务契约。
/// </summary>
public interface ITrayService
{
    event EventHandler? ShowRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? CheckUpdateRequested;

    event EventHandler? ExitRequested;

    event EventHandler? TogglePinRequested;

    void Initialize();

    void UpdateTooltip(string text);

    void ShowBalloon(string title, string message);
}
