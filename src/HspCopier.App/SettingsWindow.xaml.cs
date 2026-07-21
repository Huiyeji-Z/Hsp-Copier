namespace HspCopier.App;

using System.Windows;
using System.Windows.Controls;
using HspCopier.Core.Animations;
using HspCopier.Core.Settings;
using HspCopier.Core.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// 设置窗口。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly IUpdateService _update;
    private readonly ILogger<SettingsWindow> _logger;
    private bool _isLoading;

    public SettingsWindow(ISettingsService settings, IUpdateService update, ILogger<SettingsWindow> logger)
    {
        _settings = settings;
        _update = update;
        _logger = logger;
        // 先置 true，挡住 InitializeComponent 解析 XAML 时 Slider.Value 默认值触发的 ValueChanged
        // 否则磁盘上保存的 Opacity/MaxHistoryItems 会被 XAML 默认值覆盖
        _isLoading = true;
        InitializeComponent();

        Loaded += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        try
        {
            var s = _settings.Current;
            SelectCombo(BackdropCombo, s.Backdrop.ToString());
            OpacitySlider.Value = s.Opacity;
            SelectCombo(AnimationCombo, s.AnimationKey);
            SelectCombo(BallStyleCombo, s.BallAnimationStyle.ToString());
            MaxItemsSlider.Value = s.MaxHistoryItems;
            MaxItemsText.Text = s.MaxHistoryItems.ToString();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void MaxItemsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxItemsText != null)
        {
            MaxItemsText.Text = ((int)e.NewValue).ToString();
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        // 实时预览：拖动滑块时立即更新到主窗口
        _ = _settings.UpdateAsync(s => s.Opacity = e.NewValue);
    }

    private static void SelectCombo(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag is string t && t == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static string GetComboTag(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is string t) return t;
        return string.Empty;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var backdropTag = GetComboTag(BackdropCombo);
            var animationTag = GetComboTag(AnimationCombo);
            var ballStyleTag = GetComboTag(BallStyleCombo);
            if (string.IsNullOrEmpty(backdropTag))
            {
                MessageBox.Show("请选择背景模式", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(animationTag))
            {
                MessageBox.Show("请选择变形动画", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(ballStyleTag))
            {
                MessageBox.Show("请选择悬浮球动画风格", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveButton.IsEnabled = false;
            await _settings.UpdateAsync(s =>
            {
                s.Backdrop = Enum.Parse<BackdropMode>(backdropTag);
                s.Opacity = OpacitySlider.Value;
                s.AnimationKey = animationTag;
                s.BallAnimationStyle = Enum.Parse<BallAnimationStyle>(ballStyleTag);
                s.MaxHistoryItems = (int)MaxItemsSlider.Value;
            });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "检查中...";
        try
        {
            var info = await _update.CheckForUpdatesAsync();
            if (info == null || !info.IsNewer)
            {
                var diag = _update.GetDiagnostics();
                MessageBox.Show($"未发现可升级的新版本。\n\n--- 诊断信息 ---\n{diag}",
                    "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var msg = $"发现新版本 {info.TargetVersion}\n\n{info.ReleaseNotes}\n\n是否立即下载并应用？";
                var ok = MessageBox.Show(msg, "检查更新", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ok == MessageBoxResult.Yes)
                {
                    var progress = new Progress<int>(p => CheckUpdateButton.Content = $"下载中... {p}%");
                    await _update.DownloadAndApplyAsync(progress, System.Threading.CancellationToken.None);
                }
            }
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
            CheckUpdateButton.Content = "检查更新";
        }
    }
}
