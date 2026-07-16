namespace HspCopier.Services.DependencyInjection;

using System;
using HspCopier.Core.Animations;
using HspCopier.Core.Clipboard;
using HspCopier.Core.History;
using HspCopier.Core.Persistence;
using HspCopier.Core.Settings;
using HspCopier.Core.Tray;
using HspCopier.Core.Update;
using HspCopier.Core.Windows;
using HspCopier.Services.Clipboard;
using HspCopier.Services.History;
using HspCopier.Services.Persistence;
using HspCopier.Services.Settings;
using HspCopier.Services.Tray;
using HspCopier.Services.Update;
using HspCopier.Services.Windows;
using HspCopier.Win32.Clipboard;
using HspCopier.Win32.Dwm;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI 注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHspCopier(this IServiceCollection services)
    {
        // Core 单例配置
        services.AddSingleton<HistoryConfig>(_ => new HistoryConfig
        {
            MaxItems = 10,
        });

        // Services
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IWindowStateService, WindowStateService>();
        services.AddSingleton<IEdgeDetector, EdgeDetector>();
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<ClipboardListener>();
        services.AddSingleton<SourceAppResolver>();
        services.AddSingleton<Core.Ball.IBallWeatherService, Ball.BallWeatherService>();

        // Win32
        services.AddSingleton<IBackdropController, BackdropController>();

        // Persistence
        services.AddSingleton<IConfigRepository, ConfigRepository>();
        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<IImageStore, ImageStore>();

        // Animations - 引擎待 App 在 UI 层注入策略后再注册
        // 见 HspCopier.App.Bootstrapper

        return services;
    }
}
