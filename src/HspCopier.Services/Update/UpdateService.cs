namespace HspCopier.Services.Update;

using System;
using System.Linq;
using System.Reflection;
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
                if (_mgr == null) return null;

                if (!IsInstalled)
                {
                    _logger.LogInformation("Not installed (dev mode), skip update check");
                    return null;
                }

                StatusChanged?.Invoke(this, "正在检查更新...");

                // 调用 CheckForUpdatesAsync
                var checkMethod = _updateManagerType?.GetMethod("CheckForUpdatesAsync", Type.EmptyTypes);
                if (checkMethod == null) return null;
                var taskObj = checkMethod.Invoke(_mgr, null);
                if (taskObj == null) return null;

                // dynamic → object，避免反射 GetType() 被 binder 误解
                object updateInfoObj = await (dynamic)taskObj;
                if (updateInfoObj == null) return null;

                // 读取 TargetFullRelease
                var targetAsset = updateInfoObj.GetType().GetProperty("TargetFullRelease")?.GetValue(updateInfoObj);
                if (targetAsset == null) return null;

                // 提取版本（SemanticVersion → System.Version）
                var semVer = targetAsset.GetType().GetProperty("Version")?.GetValue(targetAsset);
                var targetVersion = ToSystemVersion(semVer);

                var result = new CoreUpdateInfo
                {
                    TargetVersion = targetVersion,
                    ReleaseNotes = GetPropertyAsString(targetAsset, "Notes") ?? string.Empty,
                    PublishedAt = GetPropertyAsDateTime(targetAsset, "PublishedAt") ?? DateTime.MinValue,
                    IsNewer = IsNewerVersion(semVer, GetCurrentVersion()),
                };

                StatusChanged?.Invoke(this, result.IsNewer
                    ? $"发现新版本 {result.TargetVersion}"
                    : "已是最新版本");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check updates failed");
                StatusChanged?.Invoke(this, "检查更新失败");
                return null;
            }
        });
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
            var gitHubSourceType = sourcesAssembly.GetTypes().FirstOrDefault(t => t.Name == "GitHubSource");
            if (gitHubSourceType == null)
            {
                _logger.LogError("GitHubSource type not found");
                return;
            }

            // 尝试两种构造签名：(owner, repo, accessToken) 或 (repoUrl, accessToken, prerelease)
            object? source = null;
            var ctors = gitHubSourceType.GetConstructors();
            foreach (var c in ctors)
            {
                var ps = c.GetParameters();
                if (ps.Length == 3 && ps[2].ParameterType == typeof(string))
                {
                    source = c.Invoke(new object?[] { AppConstants.GitHubOwner, AppConstants.GitHubRepo, null });
                    break;
                }
            }
            if (source == null)
            {
                foreach (var c in ctors)
                {
                    var ps = c.GetParameters();
                    if (ps.Length == 3)
                    {
                        var url = $"https://github.com/{AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}";
                        source = c.Invoke(new object?[] { url, null, false });
                        break;
                    }
                }
            }
            if (source == null)
            {
                _logger.LogError("GitHubSource constructor not found");
                return;
            }

            _updateManagerType = typeof(UpdateManager);
            _mgr = Activator.CreateInstance(_updateManagerType, source);
        }
        catch (Exception ex)
        {
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
