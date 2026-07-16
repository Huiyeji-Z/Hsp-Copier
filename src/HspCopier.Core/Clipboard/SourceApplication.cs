namespace HspCopier.Core.Clipboard;

/// <summary>
/// 剪贴板内容来源应用信息。
/// </summary>
public sealed record SourceApplication(
    string Name,
    string? ExecutablePath,
    uint ProcessId)
{
    public static SourceApplication Unknown(uint pid = 0) => new("Unknown", null, pid);
}
