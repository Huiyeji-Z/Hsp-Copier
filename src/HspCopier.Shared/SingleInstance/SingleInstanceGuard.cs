namespace HspCopier.Shared.SingleInstance;

using System.Threading;

/// <summary>
/// 单实例守护。基于全局命名 Mutex。
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    public bool HasOwnership { get; }

    public SingleInstanceGuard(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
        HasOwnership = createdNew;
    }

    public void Dispose()
    {
        if (HasOwnership)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
