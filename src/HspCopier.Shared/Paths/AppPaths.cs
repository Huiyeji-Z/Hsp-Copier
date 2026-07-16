namespace HspCopier.Shared.Paths;

using System.IO;

/// <summary>
/// 应用数据路径管理。基于 %APPDATA%/HspCopier/。
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> _dataRoot = new(() =>
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = Path.Combine(appData, "HspCopier");
        Directory.CreateDirectory(root);
        return root;
    });

    public static string DataRoot => _dataRoot.Value;

    public static string ConfigFile => Path.Combine(DataRoot, "config.json");

    public static string HistoryFile => Path.Combine(DataRoot, "history.json");

    public static string ImagesDir
    {
        get
        {
            var p = Path.Combine(DataRoot, "images");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    public static string BackgroundsDir
    {
        get
        {
            var p = Path.Combine(DataRoot, "backgrounds");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    public static string BallsDir
    {
        get
        {
            var p = Path.Combine(DataRoot, "balls");
            Directory.CreateDirectory(p);
            return p;
        }
    }

    public static string LogsDir
    {
        get
        {
            var p = Path.Combine(DataRoot, "logs");
            Directory.CreateDirectory(p);
            return p;
        }
    }
}
