namespace HspCopier.Core.Clipboard;

/// <summary>
/// 抽象剪贴板记录。每个记录对应一次复制行为。
/// </summary>
public abstract record ClipboardRecord(
    Guid Id,
    DateTime CopiedAt,
    SourceApplication SourceApp);

/// <summary>文本记录。</summary>
public sealed record TextRecord(
    Guid Id,
    DateTime CopiedAt,
    SourceApplication SourceApp,
    string Text,
    string TextHash) : ClipboardRecord(Id, CopiedAt, SourceApp);

/// <summary>文件记录（可能为多个文件批量复制）。</summary>
public sealed record FileRecord(
    Guid Id,
    DateTime CopiedAt,
    SourceApplication SourceApp,
    IReadOnlyList<string> FilePaths,
    string FileSetHash) : ClipboardRecord(Id, CopiedAt, SourceApp);

/// <summary>图片记录（已落盘到 images/ 目录）。</summary>
public sealed record ImageRecord(
    Guid Id,
    DateTime CopiedAt,
    SourceApplication SourceApp,
    string StoredImagePath,
    string ImageHash) : ClipboardRecord(Id, CopiedAt, SourceApp);
