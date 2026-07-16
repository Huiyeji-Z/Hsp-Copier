namespace HspCopier.Core.Update;

/// <summary>
/// 更新信息。
/// </summary>
public sealed class UpdateInfo
{
    public required Version TargetVersion { get; init; }

    public required string ReleaseNotes { get; init; }

    public required DateTime PublishedAt { get; init; }

    public required bool IsNewer { get; init; }
}

/// <summary>
/// 更新服务契约。
/// </summary>
public interface IUpdateService
{
    /// <summary>是否处于 Velopack 安装环境（开发态返回 false）。</summary>
    bool IsInstalled { get; }

    /// <summary>检查更新。</summary>
    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>下载并应用更新（fast-restart）。返回是否触发重启。</summary>
    Task<bool> DownloadAndApplyAsync(IProgress<int> progress, CancellationToken ct);

    event EventHandler<int>? DownloadProgress;

    event EventHandler<string>? StatusChanged;
}
