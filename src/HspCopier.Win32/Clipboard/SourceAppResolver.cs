namespace HspCopier.Win32.Clipboard;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using HspCopier.Core.Clipboard;
using Microsoft.Extensions.Logging;

/// <summary>
/// 解析剪贴板来源应用。基于 GetForegroundWindow → GetWindowThreadProcessId → QueryFullProcessImageName。
/// </summary>
public sealed class SourceAppResolver
{
    private readonly ILogger<SourceAppResolver> _logger;

    public SourceAppResolver(ILogger<SourceAppResolver> logger)
    {
        _logger = logger;
    }

    public SourceApplication Resolve(IntPtr hwndAtCopyTime)
    {
        try
        {
            // 优先用传入 hwnd，否则取前台窗口
            var hwnd = hwndAtCopyTime != IntPtr.Zero ? hwndAtCopyTime : GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return SourceApplication.Unknown();

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return SourceApplication.Unknown();

            var exePath = GetExecutablePath(pid);
            var name = ResolveDisplayName(exePath, pid);
            return new SourceApplication(name, exePath, pid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolve source app failed");
            return SourceApplication.Unknown();
        }
    }

    private static string GetExecutablePath(uint pid)
    {
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            // 跨权限访问失败时回退到 QueryFullProcessImageNameW
            return QueryFullProcessImageName(pid);
        }
    }

    private static string QueryFullProcessImageName(uint pid)
    {
        var hProc = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (hProc == IntPtr.Zero) return string.Empty;
        try
        {
            var buf = new char[260];
            var size = (uint)buf.Length;
            if (QueryFullProcessImageNameW(hProc, 0, buf, ref size))
            {
                return new string(buf, 0, (int)size);
            }
            return string.Empty;
        }
        finally
        {
            CloseHandle(hProc);
        }
    }

    private string ResolveDisplayName(string? exePath, uint pid)
    {
        if (string.IsNullOrEmpty(exePath)) return "Unknown";

        try
        {
            // 1. FileVersionInfo.FileDescription / ProductName
            var fvi = FileVersionInfo.GetVersionInfo(exePath);
            var name = !string.IsNullOrWhiteSpace(fvi.FileDescription) ? fvi.FileDescription
                       : !string.IsNullOrWhiteSpace(fvi.ProductName) ? fvi.ProductName
                       : Path.GetFileNameWithoutExtension(exePath);
            return name;
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(exePath);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, [Out] char[] lpExeName, ref uint lpdwSize);
}
