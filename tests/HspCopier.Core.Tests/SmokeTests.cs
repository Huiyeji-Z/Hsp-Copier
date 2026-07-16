namespace HspCopier.Core.Tests;

using HspCopier.Core.Clipboard;
using HspCopier.Core.History;
using HspCopier.Core.Settings;
using Xunit;

/// <summary>
/// 占位冒烟测试，验证 Core 模型可正常构造。
/// 后续按 §9 验证方案补充完整测试集。
/// </summary>
public class SmokeTests
{
    [Fact]
    public void SourceApplication_Unknown_ReturnsDefault()
    {
        var app = SourceApplication.Unknown();
        Assert.Equal("Unknown", app.Name);
        Assert.Null(app.ExecutablePath);
    }

    [Fact]
    public void UserSettings_Defaults_AreExpected()
    {
        var s = new UserSettings();
        Assert.Equal(BackdropMode.Acrylic, s.Backdrop);
        Assert.Equal(10, s.MaxHistoryItems);
        Assert.Equal(BallAnimationStyle.Minion, s.BallAnimationStyle);
        Assert.Equal("ScaleFade", s.AnimationKey);
    }

    [Fact]
    public void HistoryConfig_Defaults_AreExpected()
    {
        var c = new HistoryConfig();
        Assert.Equal(10, c.MaxItems);
        Assert.True(c.DeduplicateText);
    }
}
