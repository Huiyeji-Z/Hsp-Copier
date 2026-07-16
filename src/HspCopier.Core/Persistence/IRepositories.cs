namespace HspCopier.Core.Persistence;

using System.IO;
using HspCopier.Core.Clipboard;
using HspCopier.Core.Settings;

/// <summary>
/// 配置仓库契约。
/// </summary>
public interface IConfigRepository
{
    Task<UserSettings> LoadAsync();

    Task SaveAsync(UserSettings settings);
}

/// <summary>
/// 历史记录仓库契约。
/// </summary>
public interface IHistoryRepository
{
    Task<IReadOnlyList<ClipboardRecord>> LoadAsync();

    Task PersistAsync(IReadOnlyList<ClipboardRecord> records);
}

/// <summary>
/// 图片存储契约（落盘到 images/ 目录）。
/// </summary>
public interface IImageStore
{
    /// <summary>保存字节数组，返回相对文件名。</summary>
    Task<string> SaveAsync(byte[] bytes, string extension = ".png");

    Stream OpenRead(string name);

    Task DeleteAsync(string name);

    string RootDirectory { get; }
}
