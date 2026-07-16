namespace HspCopier.Services.Settings;

using System;
using System.Threading.Tasks;
using HspCopier.Core.Persistence;
using HspCopier.Core.Settings;
using Microsoft.Extensions.Logging;

/// <summary>
/// 设置服务实现。
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IConfigRepository _repo;
    private readonly ILogger<SettingsService> _logger;
    private UserSettings _current = new();

    public SettingsService(IConfigRepository repo, ILogger<SettingsService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public UserSettings Current => _current;

    public event EventHandler? Changed;

    public async Task LoadAsync()
    {
        try
        {
            _current = await _repo.LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load settings failed, use default");
            _current = new UserSettings();
        }
    }

    public async Task SaveAsync()
    {
        await _repo.SaveAsync(_current);
        RaiseChanged();
    }

    public async Task UpdateAsync(Action<UserSettings> mutator)
    {
        mutator(_current);
        await SaveAsync();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
