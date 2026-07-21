namespace HspCopier.Services.Update;

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspCopier.Core.Constants;
using HspCopier.Core.Update;
using Microsoft.Extensions.Logging;
using Velopack;

// 别名解决 Velopack.UpdateInfo 与 HspCopier.Core.Update.UpdateInfo 二义性
using CoreUpdateInfo = HspCopier.Core.Update.UpdateInfo;

/// <summary>
/// 更新服务。基于 Velopack + GitHub Releases。
/// 实现 fast-restart：后台下载 → 应用 → ~1s 快速重启 → 状态恢复。
/// 使用反射访问 Velopack API，避免对不同小版本 API 形状的硬依赖。
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private object? _mgr; // UpdateManager 实例（反射访问）
    private Type? _updateManagerType;
    private string _lastStatus = "未检查";
    private string? _lastError;
    private string? _ensureManagerError;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
    }

    public bool IsInstalled
    {
        get
        {
            EnsureManager();
            if (_mgr == null) return false;
            var prop = _updateManagerType?.GetProperty("IsInstalled");
            return prop?.GetValue(_mgr) is true;
        }
    }

    public event EventHandler<int>? DownloadProgress;

    public event EventHandler<string>? StatusChanged;

    public Task<CoreUpdateInfo?> CheckForUpdatesAsync()
    {
        return Task.Run<CoreUpdateInfo?>(async () =>
        {
            try
            {
                EnsureManager();
                if (_mgr == null)
                {
                    _lastStatus = "UpdateManager 未创建";
                    _lastError = _ensureManagerError;
                    _logger.LogError("UpdateManager is null after EnsureManager; cannot check updates");
                    StatusChanged?.Invoke(this, "检查更新失败");
                    return null;
                }

                if (!IsInstalled)
                {
                    _lastStatus = "未通过 Velopack 安装";
                    _logger.LogInformation("Not installed (dev mode or non-Velopack install), skip update check. " +
                        "Hint: client must be installed via Velopack Setup.exe, not Inno Setup, for online updates to work.");
                    StatusChanged?.Invoke(this, "未通过 Velopack 安装");
                    return null;
                }

                _lastStatus = "正在检查更新...";
                StatusChanged?.Invoke(this, "正在检查更新...");

                // 调用 CheckForUpdatesAsync
                var checkMethod = _updateManagerType?.GetMethod("CheckForUpdatesAsync", Type.EmptyTypes);
                if (checkMethod == null)
                {
                    _lastStatus = "CheckForUpdatesAsync 方法未找到";
                    _lastError = "_updateManagerType is null or method missing";
                    _logger.LogError("CheckForUpdatesAsync method not found on UpdateManager");
                    return null;
                }
                var taskObj = checkMethod.Invoke(_mgr, null);
                if (taskObj == null)
                {
                    _lastStatus = "CheckForUpdatesAsync 返回 null task";
                    _logger.LogError("CheckForUpdatesAsync returned null task");
                    return null;
                }

                // dynamic → object，避免反射 GetType() 被 binder 误解
                object updateInfoObj = await (dynamic)taskObj;
                if (updateInfoObj == null)
                {
                    _lastStatus = "已是最新版本（或 releases.win.json 未找到）";
                    _logger.LogInformation("Velopack CheckForUpdatesAsync returned null (no newer release or releases.win.json not found)");
                    StatusChanged?.Invoke(this, "已是最新版本");
                    return null;
                }

                // 读取 TargetFullRelease
                var targetAsset = updateInfoObj.GetType().GetProperty("TargetFullRelease")?.GetValue(updateInfoObj);
                if (targetAsset == null)
                {
                    _lastStatus = "TargetFullRelease 为 null";
                    return null;
                }

                // 提取版本（SemanticVersion → System.Version）
                var semVer = targetAsset.GetType().GetProperty("Version")?.GetValue(targetAsset);
                var targetVersion = ToSystemVersion(semVer);
                var currentVersion = GetCurrentVersion();
                var isNewer = IsNewerVersion(semVer, currentVersion);

                _lastError = null;

                var result = new CoreUpdateInfo
                {
                    TargetVersion = targetVersion,
                    ReleaseNotes = GetPropertyAsString(targetAsset, "Notes") ?? string.Empty,
                    PublishedAt = GetPropertyAsDateTime(targetAsset, "PublishedAt") ?? DateTime.MinValue,
                    IsNewer = isNewer,
                };

                _lastStatus = isNewer ? $"发现新版本 {result.TargetVersion}" : "已是最新版本";
                StatusChanged?.Invoke(this, _lastStatus);

                return result;
            }
            catch (Exception ex)
            {
                _lastError = ex.ToString();
                _lastStatus = "检查更新失败";
                _logger.LogError(ex, "Check updates failed");
                StatusChanged?.Invoke(this, "检查更新失败");
                return null;
            }
        });
    }

    public string GetDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== UpdateService 诊断 ===");
        sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Repo: {AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}");
        sb.AppendLine($"RepoUrl: https://github.com/{AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}");
        sb.AppendLine($"App exe: {Environment.ProcessPath ?? "(null)"}");
        sb.AppendLine();
        sb.AppendLine($"UpdateManager 已创建: {_mgr != null}");
        sb.AppendLine($"EnsureManager 错误: {_ensureManagerError ?? "(无)"}");
        sb.AppendLine($"IsInstalled: {IsInstalled}");
        sb.AppendLine($"最近状态: {_lastStatus}");
        sb.AppendLine($"最近错误: {_lastError ?? "(无)"}");

        if (_mgr != null)
        {
            try
            {
                var cur = GetCurrentVersion();
                sb.AppendLine($"CurrentVersion: {cur?.ToString() ?? "(null)"}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"CurrentVersion 读取异常: {ex.Message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== 提示 ===");
        sb.AppendLine("1. 日志位置: %APPDATA%\\HspCopier\\logs\\hspcopier-YYYYMMDD.log");
        sb.AppendLine("2. 安装位置: %LOCALAPPDATA%\\HspCopier\\（应有 Update.exe 和 current\\ 子目录）");
        sb.AppendLine("3. 若 IsInstalled=false，说明未通过 Velopack Setup.exe 安装");
        sb.AppendLine("4. 若最近状态显示\"已是最新版本（或 releases.win.json 未找到）\"，请确认 GitHub Release 中存在 releases.win.json");

        return sb.ToString();
    }

    public async Task<bool> DownloadAndApplyAsync(IProgress<int> progress, CancellationToken ct)
    {
        EnsureManager();
        if (_mgr == null || !IsInstalled) return false;

        try
        {
            var checkMethod = _updateManagerType?.GetMethod("CheckForUpdatesAsync", Type.EmptyTypes);
            if (checkMethod == null) return false;
            var taskObj = checkMethod.Invoke(_mgr, null);
            if (taskObj == null) return false;

            // 把 dynamic 转回 object，避免动态 binder 干扰反射
            object updateInfoObj = await (dynamic)taskObj;
            if (updateInfoObj == null) return false;
            var updateInfoType = updateInfoObj.GetType();

            StatusChanged?.Invoke(this, "正在下载更新...");

            // 调用 DownloadUpdatesAsync(updateInfo, Action<int>)
            var downloadMethod = _updateManagerType?.GetMethod("DownloadUpdatesAsync", new[] { updateInfoType, typeof(Action<int>) });
            if (downloadMethod == null)
            {
                _logger.LogError("DownloadUpdatesAsync method not found");
                return false;
            }

            Action<int> onProgress = p =>
            {
                progress.Report(p);
                DownloadProgress?.Invoke(this, p);
            };

            var downloadTaskObj = downloadMethod.Invoke(_mgr, new object[] { updateInfoObj, onProgress });
            if (downloadTaskObj != null)
            {
                await (dynamic)downloadTaskObj;
            }

            StatusChanged?.Invoke(this, "正在应用更新...");
            // ApplyUpdatesAndRestart(updateInfo)
            var applyMethod = _updateManagerType?.GetMethod("ApplyUpdatesAndRestart", new[] { updateInfoType });
            applyMethod?.Invoke(_mgr, new object[] { updateInfoObj });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download & apply failed");
            StatusChanged?.Invoke(this, "更新失败");
            return false;
        }
    }

    private void EnsureManager()
    {
        if (_mgr != null) return;

        try
        {
            // 构造 GitHubSource
            var sourcesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Velopack.Sources")
                ?? typeof(UpdateManager).Assembly;
            var gitHubSourceType = sourcesAssembly.GetTypes().FirstOrDefault(t => t.Name == "GithubSource");
            if (gitHubSourceType == null)
            {
                _ensureManagerError = "GithubSource type not found in assembly " + sourcesAssembly.FullName;
                _logger.LogError("GithubSource type not found");
                return;
            }

            // GithubSource 构造签名：(string repoUrl, string? accessToken, bool prerelease, IFileDownloader? downloader = null)
            var repoUrl = $"https://github.com/{AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}";
            object? source = null;
            var ctor = gitHubSourceType.GetConstructors()
                .FirstOrDefault(c => c.GetParameters().Length >= 3
                    && c.GetParameters()[0].ParameterType == typeof(string));
            if (ctor == null)
            {
                _ensureManagerError = "GithubSource constructor not found";
                _logger.LogError("GithubSource constructor not found");
                return;
            }
            var args = new List<object?> { repoUrl, null, false };
            // 第 4 个可选参数 IFileDownloader 默认 null
            while (args.Count < ctor.GetParameters().Length) args.Add(null);
            source = ctor.Invoke(args.ToArray());

            _updateManagerType = typeof(UpdateManager);
            _mgr = Activator.CreateInstance(_updateManagerType, source);
        }
        catch (Exception ex)
        {
            _ensureManagerError = ex.ToString();
            _logger.LogError(ex, "EnsureManager failed");
        }
    }

    private object? GetCurrentVersion()
    {
        if (_mgr == null) return null;
        var prop = _updateManagerType?.GetProperty("CurrentVersion");
        return prop?.GetValue(_mgr);
    }

    private static Version ToSystemVersion(object? semVer)
    {
        if (semVer == null) return new Version(0, 0, 0);
        var major = (int?)GetFieldValue(semVer, "Major") ?? 0;
        var minor = (int?)GetFieldValue(semVer, "Minor") ?? 0;
        var patch = (int?)GetFieldValue(semVer, "Patch") ?? 0;
        return new Version(major, minor, patch);
    }

    private static bool IsNewerVersion(object? target, object? current)
    {
        if (target == null || current == null) return false;
        return ToSystemVersion(target) > ToSystemVersion(current);
    }

    private static object? GetFieldValue(object obj, string name)
    {
        var t = obj.GetType();
        var field = t.GetField(name);
        if (field != null) return field.GetValue(obj);
        var prop = t.GetProperty(name);
        return prop?.GetValue(obj);
    }

    private static string? GetPropertyAsString(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name);
        return prop?.GetValue(obj) as string;
    }

    private static DateTime? GetPropertyAsDateTime(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name);
        if (prop == null) return null;
        var val = prop.GetValue(obj);
        return val switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.LocalDateTime,
            _ => null,
        };
    }
}
