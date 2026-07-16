namespace HspCopier.Win32.Window;

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

/// <summary>
/// Windows Aero Snap 抑制器。
/// 拦截 WM_SYSCOMMAND 中的 SC_MAXIMIZE，吞掉最大化命令，
/// 防止任何路径触发的全屏最大化（包括双击标题栏、Win+↑、外部调用）。
/// 拖动 Snap preview 由自定义拖动循环（不使用 DragMove）避免。
/// </summary>
public sealed class AeroSnapSuppressor : IDisposable
{
    private readonly Window _window;
    private HwndSource? _source;
    private bool _disposed;

    public AeroSnapSuppressor(Window window)
    {
        _window = window;
    }

    public void Attach()
    {
        _source = PresentationSource.FromVisual(_window) as HwndSource
                  ?? HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
        _source?.AddHook(WndProc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_SYSCOMMAND)
        {
            var cmd = wParam.ToInt32() & 0xFFF0;
            if (cmd == NativeMethods.SC_MAXIMIZE)
            {
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }
}
