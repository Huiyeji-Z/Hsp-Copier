namespace HspCopier.Shared.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using System.IO;

/// <summary>
/// 日志工厂。基于 Serilog，按日滚动到 %APPDATA%/HspCopier/logs/。
/// 注意：类名特意避开 LoggerFactory，防止与 Microsoft.Extensions.Logging.LoggerFactory 二义性。
/// </summary>
public static class AppLoggerFactory
{
    private static Logger? _logger;
    private static readonly object _sync = new();

    public static Logger Create(string logDirectory)
    {
        lock (_sync)
        {
            if (_logger != null) return _logger;

            Directory.CreateDirectory(logDirectory);

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("App", "HspCopier")
                .Filter.ByExcluding(Matching.FromSource("System"))
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "hspcopier-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            return _logger;
        }
    }

    public static void Shutdown()
    {
        lock (_sync)
        {
            _logger?.Dispose();
            _logger = null;
        }
    }
}
