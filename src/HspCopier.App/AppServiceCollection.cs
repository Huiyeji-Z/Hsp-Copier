namespace HspCopier.App;

using System.Windows;
using HspCopier.Core.Windows;
using HspCopier.Services.Windows;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App 内部 DI 注册（窗口类）。
/// </summary>
internal static class AppServiceCollection
{
    public static IServiceCollection AddAppWindows(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<BallWindow>();
        services.AddTransient<SettingsWindow>();
        return services;
    }
}
