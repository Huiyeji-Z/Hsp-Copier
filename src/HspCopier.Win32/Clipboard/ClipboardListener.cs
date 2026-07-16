namespace HspCopier.Win32.Clipboard;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using HspCopier.Core.Clipboard;
using Microsoft.Extensions.Logging;

/// <summary>
/// 剪贴板监听器。基于 AddClipboardFormatListener + WM_CLIPBOARDUPDATE。
/// 必须由 WPF 窗口在 HwndSource hook 中将 WM_CLIPBOARDUPDATE 路由到 OnClipboardUpdate。
/// </summary>
public sealed class ClipboardListener : IClipboardListener, IDisposable
{
    private readonly ILogger<ClipboardListener> _logger;
    private readonly SourceAppResolver _sourceResolver;
    private readonly Debouncer _debouncer = new(TimeSpan.FromMilliseconds(100));
    private IntPtr _hwnd;
    private bool _running;

    public ClipboardListener(ILogger<ClipboardListener> logger, SourceAppResolver sourceResolver)
    {
        _logger = logger;
        _sourceResolver = sourceResolver;
    }

    public event EventHandler<ClipboardContentChangedEventArgs>? ContentChanged;

    public bool IsRunning => _running;

    public void Start(IntPtr hwnd)
    {
        if (_running) return;
        _hwnd = hwnd;
        var ok = NativeMethods.AddClipboardFormatListener(hwnd);
        if (!ok)
        {
            _logger.LogError("AddClipboardFormatListener failed: {Err}", Marshal.GetLastWin32Error());
            return;
        }
        _running = true;
        _logger.LogInformation("Clipboard listener started");
    }

    public void Stop()
    {
        if (!_running) return;
        NativeMethods.RemoveClipboardFormatListener(_hwnd);
        _running = false;
        _logger.LogInformation("Clipboard listener stopped");
    }

    /// <summary>
    /// 由窗口 HwndSource hook 在收到 WM_CLIPBOARDUPDATE 时调用。
    /// 同步立即处理（前台窗口可能瞬间切换）。
    /// </summary>
    public void OnClipboardUpdate()
    {
        _debouncer.Schedule(() =>
        {
            try
            {
                var record = ParseClipboard();
                if (record != null)
                {
                    ContentChanged?.Invoke(this, new ClipboardContentChangedEventArgs { Record = record });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Parse clipboard failed");
            }
        });
    }

    private ClipboardRecord? ParseClipboard()
    {
        var source = _sourceResolver.Resolve(IntPtr.Zero);

        // 优先级：文件 > 图片 > 文本
        if (IsFormatAvailable(NativeMethods.CF_HDROP))
        {
            var files = ReadFileDropList();
            if (files.Count > 0)
            {
                var hash = ComputeFilesHash(files);
                return new FileRecord(Guid.NewGuid(), DateTime.Now, source, files, hash);
            }
        }

        if (IsFormatAvailable(NativeMethods.CF_DIB) || IsFormatAvailable(NativeMethods.CF_BITMAP))
        {
            // 图片走 WPF Clipboard 路径（在 UI 线程）
            // 这里只触发空 record，实际图片处理在 UI 线程
        }

        var text = ReadUnicodeText();
        if (!string.IsNullOrEmpty(text))
        {
            var hash = ComputeTextHash(text);
            return new TextRecord(Guid.NewGuid(), DateTime.Now, source, text, hash);
        }

        return null;
    }

    private static bool IsFormatAvailable(uint format)
    {
        // 简化：用 WPF.Clipboard.ContainsData 在 UI 线程；此处用 Win32 EnumClipboardFormats
        if (!NativeMethods.OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            uint fmt = 0;
            while ((fmt = NativeMethods.EnumClipboardFormats(fmt)) != 0)
            {
                if (fmt == format) return true;
            }
            return false;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static string ReadUnicodeText()
    {
        if (!NativeMethods.OpenClipboard(IntPtr.Zero)) return string.Empty;
        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return string.Empty;
            var ptr = NativeMethods.GlobalLock(handle);
            if (ptr == IntPtr.Zero) return string.Empty;
            try
            {
                var size = (int)(NativeMethods.GlobalSize(handle).ToUInt64() / 2);
                if (size <= 0) return string.Empty;
                var chars = new char[size];
                Marshal.Copy(ptr, chars, 0, size);
                var text = new string(chars).TrimEnd('\0');
                return text;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static List<string> ReadFileDropList()
    {
        var result = new List<string>();
        if (!NativeMethods.OpenClipboard(IntPtr.Zero)) return result;
        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_HDROP);
            if (handle == IntPtr.Zero) return result;
            var ptr = NativeMethods.GlobalLock(handle);
            if (ptr == IntPtr.Zero) return result;
            try
            {
                // HDROP 结构：第一个 UINT 是文件数，后面是 WCHAR 路径列表
                int count = Marshal.ReadInt32(ptr);
                for (int i = 0; i < count; i++)
                {
                    // DragQueryList 简化：调用 shell32
                    var sb = new StringBuilder(260);
                    int len = SHDragQueryFile(handle, i, sb, sb.Capacity);
                    if (len > 0) result.Add(sb.ToString());
                }
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
        return result;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int DragQueryFile(IntPtr hDrop, int iFile, StringBuilder lpszFile, int cch);

    private static int SHDragQueryFile(IntPtr hDrop, int i, StringBuilder sb, int cap) => DragQueryFile(hDrop, i, sb, cap);

    private static string ComputeTextHash(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static string ComputeFilesHash(IReadOnlyList<string> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(x => x))
        {
            var info = new FileInfo(f);
            sb.Append(f).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append(';');
        }
        return ComputeTextHash(sb.ToString());
    }

    public void Dispose()
    {
        Stop();
        _debouncer.Dispose();
    }
}

/// <summary>
/// 防抖器：合并多次剪贴板写入事件。
/// </summary>
internal sealed class Debouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private Timer? _timer;
    private Action? _pending;
    private readonly object _sync = new();

    public Debouncer(TimeSpan delay) => _delay = delay;

    public void Schedule(Action action)
    {
        lock (_sync)
        {
            _pending = action;
            _timer?.Dispose();
            _timer = new Timer(_ =>
            {
                Action? toRun;
                lock (_sync)
                {
                    toRun = _pending;
                    _pending = null;
                }
                toRun?.Invoke();
            }, null, _delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
