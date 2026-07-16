namespace HspCopier.Services.Clipboard;

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using HspCopier.Core.Clipboard;
using HspCopier.Core.History;
using HspCopier.Win32.Clipboard;
using HspCopier.Win32.Window;
using Microsoft.Extensions.Logging;

/// <summary>
/// 剪贴板协调服务：将 Win32 监听器的事件路由到历史记录服务。
/// 同时负责将记录置入剪贴板（点击复制时）。
/// </summary>
public sealed class ClipboardService : IDisposable
{
    private readonly ClipboardListener _listener;
    private readonly IHistoryService _history;
    private readonly ILogger<ClipboardService> _logger;
    private ClipboardHookHandler? _hookHandler;

    public ClipboardService(
        ClipboardListener listener,
        IHistoryService history,
        ILogger<ClipboardService> logger)
    {
        _listener = listener;
        _history = history;
        _logger = logger;
    }

    public void Start(IntPtr hwnd)
    {
        _listener.ContentChanged += OnContentChanged;
        _listener.Start(hwnd);
        _hookHandler = new ClipboardHookHandler(hwnd, _listener.OnClipboardUpdate);
        _hookHandler.Attach();
    }

    private void OnContentChanged(object? sender, ClipboardContentChangedEventArgs e)
    {
        _ = _history.AddAsync(e.Record);
    }

    /// <summary>
    /// 点击记录：复制内容到剪贴板（避免触发自身监听的循环）。
    /// </summary>
    public void CopyToClipboard(ClipboardRecord record)
    {
        // 临时停止监听以避免监听到自己的写入
        _listener.Stop();
        try
        {
            switch (record)
            {
                case TextRecord t:
                    Clipboard.SetText(t.Text);
                    break;
                case FileRecord f:
                    var files = new System.Collections.Specialized.StringCollection();
                    files.AddRange(f.FilePaths.ToArray());
                    Clipboard.SetFileDropList(files);
                    break;
                case ImageRecord i:
                    var bmp = new System.Windows.Media.Imaging.BitmapImage(new Uri(i.StoredImagePath));
                    Clipboard.SetImage(bmp);
                    break;
            }
        }
        finally
        {
            // 重新挂载监听
            var hwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            _listener.Start(hwnd);
            _hookHandler = new ClipboardHookHandler(hwnd, _listener.OnClipboardUpdate);
            _hookHandler.Attach();
        }
    }

    public void Dispose()
    {
        _listener.ContentChanged -= OnContentChanged;
        _hookHandler?.Detach();
        _listener.Dispose();
    }
}
