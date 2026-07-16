namespace HspCopier.Services.History;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HspCopier.Core.Clipboard;
using HspCopier.Core.History;
using HspCopier.Core.Persistence;
using Microsoft.Extensions.Logging;

/// <summary>
/// 历史记录服务。维护内存集合 + 持久化。
/// </summary>
public sealed class HistoryService : IHistoryService
{
    private readonly IHistoryRepository _repo;
    private readonly HistoryConfig _config;
    private readonly ILogger<HistoryService> _logger;
    private readonly List<ClipboardRecord> _items = new();
    private readonly object _sync = new();

    public HistoryService(IHistoryRepository repo, HistoryConfig config, ILogger<HistoryService> logger)
    {
        _repo = repo;
        _config = config;
        _logger = logger;
    }

    public IReadOnlyList<ClipboardRecord> Items
    {
        get
        {
            lock (_sync) return _items.ToList();
        }
    }

    public event EventHandler? ItemsChanged;

    public async Task LoadAsync()
    {
        var loaded = await _repo.LoadAsync();
        lock (_sync)
        {
            _items.Clear();
            _items.AddRange(loaded);
            EnforceCap();
        }
        RaiseChanged();
    }

    public async Task AddAsync(ClipboardRecord record)
    {
        // 去重
        var hash = GetHash(record);
        if (!string.IsNullOrEmpty(hash))
        {
            var dup = FindDuplicateInternal(hash);
            if (dup != null)
            {
                // 重复记录：始终重排到顶部
                await ReorderToTopInternalAsync(dup.Id);
                await PersistAsync();
                RaiseChanged();
                return;
            }
        }

        lock (_sync)
        {
            _items.Insert(0, record);
            EnforceCap();
        }
        await PersistAsync();
        RaiseChanged();
    }

    public async Task RemoveAsync(Guid id)
    {
        lock (_sync)
        {
            _items.RemoveAll(x => x.Id == id);
        }
        await PersistAsync();
        RaiseChanged();
    }

    public async Task ClearAsync()
    {
        lock (_sync) _items.Clear();
        await PersistAsync();
        RaiseChanged();
    }

    public async Task ReorderToTopAsync(Guid id)
    {
        await ReorderToTopInternalAsync(id);
        await PersistAsync();
        RaiseChanged();
    }

    public ClipboardRecord? FindDuplicate(string hash)
    {
        lock (_sync) return FindDuplicateInternal(hash);
    }

    private ClipboardRecord? FindDuplicateInternal(string hash)
    {
        return _items.FirstOrDefault(r => string.Equals(GetHash(r), hash, StringComparison.Ordinal));
    }

    private async Task ReorderToTopInternalAsync(Guid id)
    {
        ClipboardRecord? item;
        lock (_sync)
        {
            item = _items.FirstOrDefault(x => x.Id == id);
            if (item == null) return;
            _items.Remove(item);
            _items.Insert(0, item);
        }
        await Task.CompletedTask;
    }

    private static string? GetHash(ClipboardRecord r) => r switch
    {
        TextRecord t => t.TextHash,
        FileRecord f => f.FileSetHash,
        ImageRecord i => i.ImageHash,
        _ => null,
    };

    private void EnforceCap()
    {
        while (_items.Count > _config.MaxItems)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    private async Task PersistAsync()
    {
        IReadOnlyList<ClipboardRecord> snapshot;
        lock (_sync) snapshot = _items.ToList();
        await _repo.PersistAsync(snapshot);
    }

    private void RaiseChanged() => ItemsChanged?.Invoke(this, EventArgs.Empty);
}
