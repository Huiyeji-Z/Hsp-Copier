namespace HspCopier.Core.History;

using HspCopier.Core.Clipboard;

/// <summary>
/// 历史记录配置。
/// </summary>
public sealed class HistoryConfig
{
    /// <summary>最大保留条数，默认 10。</summary>
    public int MaxItems { get; set; } = 10;

    /// <summary>是否启用文本去重，默认 true。</summary>
    public bool DeduplicateText { get; set; } = true;
}

/// <summary>
/// 历史记录服务契约。
/// </summary>
public interface IHistoryService
{
    IReadOnlyList<ClipboardRecord> Items { get; }

    event EventHandler? ItemsChanged;

    Task AddAsync(ClipboardRecord record);

    Task RemoveAsync(Guid id);

    Task ClearAsync();

    /// <summary>点击复制后调用：可选重排到顶部。</summary>
    Task ReorderToTopAsync(Guid id);

    /// <summary>根据 hash 查询是否已存在重复记录。</summary>
    ClipboardRecord? FindDuplicate(string hash);
}
