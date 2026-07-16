namespace HspCopier.Services.Tray;

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HspCopier.Core.Tray;
using Microsoft.Extensions.Logging;

/// <summary>
/// 系统托盘服务。基于 H.NotifyIcon。
/// 注意：实际 NotifyIcon 实例需在 UI 线程创建，此处提供事件契约。
/// </summary>
public sealed class TrayService : ITrayService, IDisposable
{
    private readonly ILogger<TrayService> _logger;
    private H.NotifyIcon.TaskbarIcon? _icon;

    public TrayService(ILogger<TrayService> logger)
    {
        _logger = logger;
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? CheckUpdateRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? TogglePinRequested;

    public void Initialize()
    {
        // 创建托盘图标（在 UI 线程）
        _icon = new H.NotifyIcon.TaskbarIcon
        {
            ToolTipText = "Hsp Copier",
            ContextMenu = BuildMenu(),
        };

        // 图标：尝试加载嵌入资源 hspcopier.ico
        try
        {
            var iconUri = new Uri("pack://application:,,,/HspCopier;component/Assets/hspcopier.ico", UriKind.Absolute);
            _icon.IconSource = new IconBitmapDecoder(
                Application.GetResourceStream(iconUri).Stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad).Frames[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load tray icon failed, using default");
        }

        _icon.TrayMouseDoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Tray initialized");
    }

    private System.Windows.Controls.ContextMenu BuildMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var miShow = new System.Windows.Controls.MenuItem { Header = "显示窗口" };
        miShow.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);

        var miPin = new System.Windows.Controls.MenuItem { Header = "切换固定", IsCheckable = true };
        miPin.Click += (_, _) => TogglePinRequested?.Invoke(this, EventArgs.Empty);

        var miSettings = new System.Windows.Controls.MenuItem { Header = "设置..." };
        miSettings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var miUpdate = new System.Windows.Controls.MenuItem { Header = "检查更新" };
        miUpdate.Click += (_, _) => CheckUpdateRequested?.Invoke(this, EventArgs.Empty);

        var miExit = new System.Windows.Controls.MenuItem { Header = "退出" };
        miExit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(miShow);
        menu.Items.Add(miPin);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(miSettings);
        menu.Items.Add(miUpdate);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(miExit);

        return menu;
    }

    public void UpdateTooltip(string text)
    {
        if (_icon != null) _icon.ToolTipText = text;
    }

    public void ShowBalloon(string title, string message)
    {
        _icon?.ShowNotification(title, message);
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
