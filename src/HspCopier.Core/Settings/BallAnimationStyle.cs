namespace HspCopier.Core.Settings;

/// <summary>
/// 悬浮球动态背景风格。
/// </summary>
public enum BallAnimationStyle
{
    /// <summary>卡通形象(小黄人):零联网,本地随机动画。</summary>
    Minion,

    /// <summary>天气动画:每 30 秒检测网络/定位/天气,显示对应场景。</summary>
    Weather,
}
