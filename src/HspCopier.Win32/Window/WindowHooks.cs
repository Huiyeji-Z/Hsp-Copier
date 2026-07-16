namespace HspCopier.Win32.Window;

using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HspCopier.Core.Windows;

/// <summary>
/// 透明置顶窗口鼠标穿透处理器。在 WM_NCHITTEST 中：
/// - 圆形悬浮球内返回 HTCLIENT（可交互）
/// - 圆外返回 HTTRANSPARENT（穿透到下层窗口）
/// </summary>
public sealed class TransparencyHitTestHandler
{
    private readonly Window _window;
    private readonly Func<Rect> _interactiveRectProvider;
    private HwndSource? _source;

    public TransparencyHitTestHandler(Window window, Func<Rect> interactiveRectProvider)
    {
        _window = window;
        _interactiveRectProvider = interactiveRectProvider;
    }

    public void Attach()
    {
        _source = PresentationSource.FromVisual(_window) as HwndSource;
        if (_source == null)
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
        }
        _source?.AddHook(WndProc);
    }

    public void Detach()
    {
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_NCHITTEST)
        {
            var x = (short)(lParam.ToInt32() & 0xFFFF);
            var y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

            // 转换为窗口坐标
            var screen = new Point(x, y);
            var windowPos = _window.PointFromScreen(screen);
            var rect = _interactiveRectProvider();

            if (rect.Contains(windowPos))
            {
                handled = true;
                return (IntPtr)NativeMethods.HTCLIENT;
            }
            else
            {
                handled = true;
                return (IntPtr)NativeMethods.HTTRANSPARENT;
            }
        }
        return IntPtr.Zero;
    }
}

/// <summary>
/// 将 WM_CLIPBOARDUPDATE 路由到剪贴板监听器。
/// </summary>
public sealed class ClipboardHookHandler
{
    private readonly Action _onClipboardUpdate;
    private HwndSource? _source;

    public ClipboardHookHandler(IntPtr hwnd, Action onClipboardUpdate)
    {
        _onClipboardUpdate = onClipboardUpdate;
        _source = HwndSource.FromHwnd(hwnd);
    }

    public void Attach()
    {
        _source?.AddHook(WndProc);
    }

    public void Detach()
    {
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HspCopier.Win32.Clipboard.NativeMethods.WM_CLIPBOARDUPDATE)
        {
            _onClipboardUpdate();
        }
        return IntPtr.Zero;
    }
}
