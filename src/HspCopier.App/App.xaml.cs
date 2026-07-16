namespace HspCopier.App;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using HspCopier.Animations.DependencyInjection;
using HspCopier.Core.Constants;
using HspCopier.Services.Clipboard;
using HspCopier.Services.DependencyInjection;
using HspCopier.Shared.Logging;
using HspCopier.Shared.Paths;
using HspCopier.Shared.SingleInstance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Velopack;

/// <summary>
/// App 入口。
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;
    private ClipboardService? _clipboardService;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Velopack 初始化：必须在 App 启动早期
        VelopackApp.Build().Run();

        // 单实例
        _singleInstance = new SingleInstanceGuard(AppConstants.SingleInstanceMutexName);
        if (!_singleInstance.HasOwnership)
        {
            MessageBox.Show("Hsp Copier 已经在运行中。", AppConstants.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // 日志
        AppLoggerFactory.Create(AppPaths.LogsDir);
        Log.Logger.Information("{App} starting up...", AppConstants.AppName);

        // 全局未处理异常捕获
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Logger.Fatal(args.ExceptionObject as Exception, "AppDomain unhandled exception");
        };
        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, args) =>
        {
            Log.Logger.Fatal(args.Exception, "Dispatcher unhandled exception");
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Logger.Fatal(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        // DI
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                services.AddHspCopier();
                services.AddHspCopierAnimations();
                services.AddAppWindows();
            })
            .Build();

        // 启动服务（异步，不阻塞 UI）
        var settings = _host.Services.GetRequiredService<HspCopier.Core.Settings.ISettingsService>();
        await settings.LoadAsync();

        var history = _host.Services.GetRequiredService<HspCopier.Core.History.IHistoryService>();
        if (history is HspCopier.Services.History.HistoryService hs)
        {
            await hs.LoadAsync();
        }

        // 应用动画 key 到引擎
        var engine = _host.Services.GetRequiredService<HspCopier.Core.Animations.IAnimationEngine>();
        engine.SetStrategy(settings.Current.AnimationKey);

        // 托盘
        var tray = _host.Services.GetRequiredService<HspCopier.Core.Tray.ITrayService>();
        tray.Initialize();

        // 主窗口
        _mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = _mainWindow;

        // 在 SourceInitialized（HWND 就绪）后启动剪贴板监听
        _mainWindow.SourceInitialized += OnMainWindowSourceInitialized;
        _mainWindow.Closed += OnMainWindowClosed;

        _mainWindow.Show();

        Log.Logger.Information("{App} started", AppConstants.AppName);
    }

    private void OnMainWindowSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(_mainWindow).Handle;
            _clipboardService = _host?.Services.GetRequiredService<ClipboardService>();
            _clipboardService?.Start(hwnd);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "ClipboardService start failed");
        }
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        // 主窗口关闭后退出应用
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _clipboardService?.Dispose();
            _host?.Dispose();
            _singleInstance?.Dispose();
            Log.Logger.Information("{App} exited", AppConstants.AppName);
            AppLoggerFactory.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Exit error");
        }
        base.OnExit(e);
    }
}
