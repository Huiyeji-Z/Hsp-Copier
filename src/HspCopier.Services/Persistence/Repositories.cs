namespace HspCopier.Services.Persistence;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HspCopier.Core.Clipboard;
using HspCopier.Core.Persistence;
using HspCopier.Core.Settings;
using HspCopier.Shared.Paths;
using Microsoft.Extensions.Logging;

/// <summary>
/// 配置仓库：JSON 原子写。
/// </summary>
public sealed class ConfigRepository : IConfigRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger<ConfigRepository> _logger;

    public ConfigRepository(ILogger<ConfigRepository> logger) => _logger = logger;

    public async Task<UserSettings> LoadAsync()
    {
        if (!File.Exists(AppPaths.ConfigFile)) return new UserSettings();
        var json = await File.ReadAllTextAsync(AppPaths.ConfigFile);
        return JsonSerializer.Deserialize<UserSettings>(json, JsonOpts) ?? new UserSettings();
    }

    public async Task SaveAsync(UserSettings settings)
    {
        var tmp = AppPaths.ConfigFile + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        await File.WriteAllTextAsync(tmp, json);
        if (File.Exists(AppPaths.ConfigFile))
            File.Replace(tmp, AppPaths.ConfigFile, destinationBackupFileName: null);
        else
            File.Move(tmp, AppPaths.ConfigFile);
        _logger.LogInformation("Settings saved");
    }
}

/// <summary>
/// 历史记录仓库：JSON 原子写。支持多态序列化 ClipboardRecord 子类型。
/// </summary>
public sealed class HistoryRepository : IHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(),
            new ClipboardRecordJsonConverter(),
        },
    };

    private readonly ILogger<HistoryRepository> _logger;

    public HistoryRepository(ILogger<HistoryRepository> logger) => _logger = logger;

    public async Task<IReadOnlyList<ClipboardRecord>> LoadAsync()
    {
        if (!File.Exists(AppPaths.HistoryFile)) return Array.Empty<ClipboardRecord>();
        var json = await File.ReadAllTextAsync(AppPaths.HistoryFile);
        return JsonSerializer.Deserialize<List<ClipboardRecord>>(json, JsonOpts) ?? new List<ClipboardRecord>();
    }

    public async Task PersistAsync(IReadOnlyList<ClipboardRecord> records)
    {
        var tmp = AppPaths.HistoryFile + ".tmp";
        var json = JsonSerializer.Serialize(records, JsonOpts);
        await File.WriteAllTextAsync(tmp, json);
        if (File.Exists(AppPaths.HistoryFile))
            File.Replace(tmp, AppPaths.HistoryFile, destinationBackupFileName: null);
        else
            File.Move(tmp, AppPaths.HistoryFile);
        _logger.LogDebug("History persisted ({Count} items)", records.Count);
    }
}

/// <summary>
/// 多态 ClipboardRecord JSON 转换器。
/// 注意：Write 内必须用 System.Text.Json 默认选项序列化具体子类型，避免再次触发本 Converter 导致递归栈溢出。
/// </summary>
internal sealed class ClipboardRecordJsonConverter : JsonConverter<ClipboardRecord>
{
    // 写入用的默认选项（不含本 Converter，避免递归）
    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public override ClipboardRecord? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (!root.TryGetProperty("$type", out var typeProp)) return null;

        // 读取用同一组 options（含本 Converter），但具体子类型反序列化时通过 GetRawText + 默认选项避免歧义
        var readOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        return typeProp.GetString() switch
        {
            "text" => JsonSerializer.Deserialize<TextRecord>(root.GetRawText(), readOpts),
            "file" => JsonSerializer.Deserialize<FileRecord>(root.GetRawText(), readOpts),
            "image" => JsonSerializer.Deserialize<ImageRecord>(root.GetRawText(), readOpts),
            _ => null,
        };
    }

    public override void Write(Utf8JsonWriter writer, ClipboardRecord value, JsonSerializerOptions options)
    {
        // 用不含本 Converter 的选项序列化具体子类型，避免递归
        var json = value switch
        {
            TextRecord t => JsonSerializer.Serialize(t, typeof(TextRecord), WriteOpts),
            FileRecord f => JsonSerializer.Serialize(f, typeof(FileRecord), WriteOpts),
            ImageRecord i => JsonSerializer.Serialize(i, typeof(ImageRecord), WriteOpts),
            _ => JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), WriteOpts),
        };

        // 解析回 JsonDocument 后再添加 $type 字段并写出
        using var doc = JsonDocument.Parse(json);
        writer.WriteStartObject();
        writer.WriteString("$type", value switch
        {
            TextRecord => "text",
            FileRecord => "file",
            ImageRecord => "image",
            _ => "unknown",
        });
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// 图片存储实现。返回绝对路径。
/// </summary>
public sealed class ImageStore : IImageStore
{
    public string RootDirectory => AppPaths.ImagesDir;

    public async Task<string> SaveAsync(byte[] bytes, string extension = ".png")
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        var name = hash + extension;
        var path = Path.Combine(RootDirectory, name);
        if (!File.Exists(path))
        {
            await File.WriteAllBytesAsync(path, bytes);
        }
        // 返回绝对路径
        return path;
    }

    public Stream OpenRead(string name)
    {
        var path = Path.IsPathRooted(name) ? name : Path.Combine(RootDirectory, name);
        return File.OpenRead(path);
    }

    public Task DeleteAsync(string name)
    {
        var path = Path.IsPathRooted(name) ? name : Path.Combine(RootDirectory, name);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
