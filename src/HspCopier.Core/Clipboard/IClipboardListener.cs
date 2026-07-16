namespace HspCopier.Core.Clipboard;

/// <summary>
/// 剪贴板内容变化事件参数。
/// </summary>
public sealed class ClipboardContentChangedEventArgs : EventArgs
{
    public required ClipboardRecord Record { get; init; }
}

/// <summary>
/// 剪贴板监听器契约。基于 Win32 AddClipboardFormatListener + WM_CLIPBOARDUPDATE。
/// </summary>
public interface IClipboardListener : IDisposable
{
    event EventHandler<ClipboardContentChangedEventArgs>? ContentChanged;

    /// <summary>启动监听。hwnd 为接收 WM_CLIPBOARDUPDATE 的窗口句柄。</summary>
    void Start(IntPtr hwnd);

    void Stop();

    bool IsRunning { get; }
}
