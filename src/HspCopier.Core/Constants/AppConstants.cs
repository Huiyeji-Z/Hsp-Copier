namespace HspCopier.Core.Constants;

/// <summary>
/// 应用全局常量。
/// </summary>
public static class AppConstants
{
    public const string AppName = "Hsp Copier";

    public const string AppId = "HspCopier";

    /// <summary>单实例 Mutex 名。</summary>
    public const string SingleInstanceMutexName = "Global\\HspCopier.SingleInstance.Mutex";

    /// <summary>GitHub 仓库 owner/repo(更新源)。</summary>
    public const string GitHubOwner = "Huiyeji-Z";

    public const string GitHubRepo = "Hsp-Copier";

    /// <summary>数据根目录（相对 %APPDATA%）。</summary>
    public const string DataDirName = "HspCopier";

    public const string ConfigFileName = "config.json";

    public const string HistoryFileName = "history.json";

    public const string ImagesDirName = "images";

    public const string BackgroundsDirName = "backgrounds";

    public const string BallsDirName = "balls";

    public const string LogsDirName = "logs";

    public const string LogFileName = "hspcopier.log";
}
