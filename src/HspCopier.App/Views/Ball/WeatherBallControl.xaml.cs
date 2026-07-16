namespace HspCopier.App.Views.Ball;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using HspCopier.Core.Ball;

/// <summary>
/// 天气球容器:
/// - 双层 ContentControl(OldLayer/NewLayer)做 CrossFade 切换
/// - 监听 IBallWeatherService.Changed,在 UI 线程上 CrossFade 到新天气子动画
/// </summary>
public partial class WeatherBallControl : UserControl
{
    private readonly IBallWeatherService _weather;

    public WeatherBallControl(IBallWeatherService weather)
    {
        _weather = weather;
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _weather.Changed += OnWeatherChanged;
        // 立即应用一次当前状态(若服务还没数据,显示 NoNetwork 兜底)
        ApplyCondition(_weather.Current, animate: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _weather.Changed -= OnWeatherChanged;
    }

    private void OnWeatherChanged(object? sender, EventArgs e)
    {
        // Changed 已在 UI 线程触发(由 BallWeatherService 保证),直接执行
        ApplyCondition(_weather.Current, animate: true);
    }

    private void ApplyCondition(WeatherCondition cond, bool animate)
    {
        var newContent = CreateAnimation(cond);
        if (newContent == null) return;

        if (!animate || NewLayer.Content == null)
        {
            // 首次填充:直接放到新层
            NewLayer.Content = newContent;
            NewLayer.Opacity = 1;
            OldLayer.Content = null;
            OldLayer.Opacity = 0;
            return;
        }

        // CrossFade:
        // 1. 旧内容搬到 OldLayer(保持 Opacity=1)
        // 2. 新内容放到 NewLayer(Opacity=0)
        // 3. Storyboard:OldLayer 1→0、NewLayer 0→1
        OldLayer.Content = NewLayer.Content;
        OldLayer.Opacity = 1;
        NewLayer.Content = newContent;
        NewLayer.Opacity = 0;

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        fadeOut.Completed += (_, _) =>
        {
            // 完成后清空旧层,减少内存占用
            OldLayer.Content = null;
            OldLayer.Opacity = 0;
        };
        OldLayer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        NewLayer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private static FrameworkElement? CreateAnimation(WeatherCondition cond) => cond switch
    {
        WeatherCondition.Sunny => new Weather.SunnyAnimation(),
        WeatherCondition.Cloudy => new Weather.CloudyAnimation(),
        WeatherCondition.Rainy => new Weather.RainyAnimation(),
        WeatherCondition.Snowy => new Weather.SnowyAnimation(),
        WeatherCondition.Thunder => new Weather.ThunderAnimation(),
        WeatherCondition.Foggy => new Weather.FoggyAnimation(),
        WeatherCondition.NightClear => new Weather.NightClearAnimation(),
        WeatherCondition.NoNetwork => new Weather.NoNetworkAnimation(),
        WeatherCondition.NoLocation => new Weather.NoLocationAnimation(),
        // Unknown 或首次未刷新:显示 NoNetwork 占位
        _ => new Weather.NoNetworkAnimation(),
    };
}
