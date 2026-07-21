namespace HspCopier.App;

using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HspCopier.Animations;
using HspCopier.Core.Animations;
using HspCopier.Core.Clipboard;
using HspCopier.Core.History;
using HspCopier.Core.Settings;
using HspCopier.Core.Windows;
using HspCopier.Services.Clipboard;
using HspCopier.Services.Windows;
using HspCopier.Win32.Dwm;
using HspCopier.Win32.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// 主窗口。透明置顶 + 自绘 chrome + 圆角 + 拖动 + 缩放。
/// </summary>
public partial class MainWindow : Window
{
    private readonly IWindowStateService _windowState;
    private readonly IHistoryService _history;
    private readonly ISettingsService _settings;
    private readonly IAnimationEngine _animationEngine;
    private readonly ClipboardService _clipboardService;
    private readonly IBackdropController _backdrop;
    private readonly ILogger<MainWindow> _logger;
    private readonly IServiceProvider _services;
    private readonly IEdgeDetector _edgeDetector;
    private BallWindow? _ballWindow;
    private AeroSnapSuppressor? _snapSuppressor;
    private bool _isDragging;
    private double _dragOffsetX;
    private double _dragOffsetY;
    private DateTime _lastCloseClickTime;
    private System.Windows.Threading.DispatcherTimer? _closeHintTimer;
    private DateTime _lastClearClickTime;
    private System.Windows.Threading.DispatcherTimer? _clearHintTimer;

    public MainWindow(
        IWindowStateService windowState,
        IHistoryService history,
        ISettingsService settings,
        IAnimationEngine animationEngine,
        ClipboardService clipboardService,
        IBackdropController backdrop,
        IEdgeDetector edgeDetector,
        ILogger<MainWindow> logger,
        IServiceProvider services)
    {
        _windowState = windowState;
        _history = history;
        _settings = settings;
        _animationEngine = animationEngine;
        _clipboardService = clipboardService;
        _backdrop = backdrop;
        _edgeDetector = edgeDetector;
        _logger = logger;
        _services = services;
        InitializeComponent();

        // 显示版本号（优先使用 AssemblyInformationalVersion，可带 -alpha 后缀）
        var asm = Assembly.GetEntryAssembly();
        var ver = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                  ?? asm?.GetName().Version?.ToString()
                  ?? "0.0.0";
        // 去掉源码 commit hash 后缀（+后面的部分）
        var plus = ver.IndexOf('+');
        if (plus >= 0) ver = ver.Substring(0, plus);
        VersionText.Text = "v" + ver;

        // 恢复上次窗口位置/尺寸（在 Show 之前设置，避免闪烁）
        RestoreWindowBounds();

        // 动画引擎注入视图引用
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.ExpandedView = RootBorder;
            ae.ExpandedRect = new Rect(Left, Top, Width, Height);
        }

        _history.ItemsChanged += OnHistoryChanged;
        _settings.Changed += OnSettingsChanged;
        _windowState.StateChanged += OnWindowStateStateChanged;
        _windowState.AnimationCompleted += OnWindowAnimationCompleted;

        Loaded += (_, _) => ApplySettingsToWindow();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _backdrop.Apply(hwnd, _settings.Current.Backdrop, _settings.Current.Opacity);

        // 接入 Aero Snap 抑制器：阻止 Windows 把窗口吸附成半屏/全屏
        _snapSuppressor = new AeroSnapSuppressor(this);
        _snapSuppressor.Attach();

        // 初始化圆角裁剪区域
        RootClip.Rect = new Rect(0, 0, ActualWidth, ActualHeight);

        // 初始化固定按钮外观（默认未固定，半透明）
        PinButton.Opacity = _windowState.Pin == PinState.Pinned ? 1.0 : 0.5;

        // 注入动画引擎的几何
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.ExpandedRect = new Rect(Left, Top, Width, Height);
            ae.BallRect = ComputeBallRectAtEdge();
        }

        RefreshHistory();
    }

    /// <summary>
    /// 根据当前窗口位置贴近的边缘，计算悬浮球应出现的位置（贴边侧）。
    /// </summary>
    private Rect ComputeBallRectAtEdge()
    {
        const double ballSize = 64;
        var edge = _edgeDetector.DetectEdge(new Rect(Left, Top, Width, Height));
        var workArea = SystemParameters.WorkArea;
        var cx = Left + Width / 2;
        var cy = Top + Height / 2;

        return edge switch
        {
            EdgeSide.Left => new Rect(workArea.Left + 4, cy - ballSize / 2, ballSize, ballSize),
            EdgeSide.Right => new Rect(workArea.Right - ballSize - 4, cy - ballSize / 2, ballSize, ballSize),
            EdgeSide.Top => new Rect(cx - ballSize / 2, workArea.Top + 4, ballSize, ballSize),
            EdgeSide.Bottom => new Rect(cx - ballSize / 2, workArea.Bottom - ballSize - 4, ballSize, ballSize),
            _ => new Rect(Left + 4, Top + Height - ballSize - 4, ballSize, ballSize),
        };
    }

    /// <summary>
    /// 标题栏鼠标左键按下 → 进入自定义拖动循环。
    /// 不使用 DragMove（会触发 Windows Aero Snap preview）。
    /// </summary>
    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginCustomDrag(e);
    }

    /// <summary>
    /// 窗口空白区鼠标左键按下 → 也允许拖动。
    /// </summary>
    private void MainWindow_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Media.Visual
            && ReferenceEquals(e.OriginalSource, RootBorder))
        {
            BeginCustomDrag(e);
        }
    }

    /// <summary>
    /// 启动自定义拖动：记录鼠标在窗口内的偏移，捕获鼠标。
    /// </summary>
    private void BeginCustomDrag(MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(this);
        _dragOffsetX = pos.X;
        _dragOffsetY = pos.Y;
        _isDragging = true;
        CaptureMouse();
    }

    /// <summary>
    /// 自定义拖动循环：根据鼠标屏幕坐标计算窗口新位置，并限制在 workArea 内。
    /// </summary>
    private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndCustomDrag();
            return;
        }

        var screen = PointToScreen(e.GetPosition(this));
        var workArea = SystemParameters.WorkArea;

        var newLeft = screen.X - _dragOffsetX;
        var newTop = screen.Y - _dragOffsetY;

        // 限制窗口完全在屏幕工作区内：贴顶部、不超出任何边
        newLeft = Math.Max(workArea.Left, Math.Min(newLeft, workArea.Right - ActualWidth));
        newTop = Math.Max(workArea.Top, Math.Min(newTop, workArea.Bottom - ActualHeight));

        Left = newLeft;
        Top = newTop;
    }

    private void MainWindow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndCustomDrag();
    }

    private void EndCustomDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void MainWindow_OnLocationChanged(object? sender, EventArgs e)
    {
        var loc = new Rect(Left, Top, Width, Height);
        _windowState.UpdateWindowLocation(loc);
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.ExpandedRect = loc;
            if (_windowState.Current == WindowVisualState.Expanded)
            {
                ae.BallRect = ComputeBallRectAtEdge();
            }
        }
        // 持久化窗口位置（仅在展开状态，避免记录动画中的瞬时坐标）
        if (_windowState.Current == WindowVisualState.Expanded)
        {
            SaveWindowBounds();
        }
    }

    private void MainWindow_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var loc = new Rect(Left, Top, Width, Height);
        _windowState.UpdateWindowLocation(loc);
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.ExpandedRect = loc;
        }
        // 同步裁剪几何到当前尺寸，确保子内容也被圆角裁剪
        RootClip.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
        // 持久化尺寸
        if (_windowState.Current == WindowVisualState.Expanded)
        {
            SaveWindowBounds();
        }
    }

    /// <summary>
    /// 恢复上次保存的窗口位置/尺寸。需在 Show 之前调用，避免视觉闪烁。
    /// 越界（多屏切换/分辨率变化后）会自动夹回工作区。
    /// </summary>
    private void RestoreWindowBounds()
    {
        var s = _settings.Current;
        if (s.WindowWidth is { } w && w >= MinWidth) Width = w;
        if (s.WindowHeight is { } h && h >= MinHeight) Height = h;
        if (s.WindowLeft is { } l && s.WindowTop is { } t)
        {
            var wa = SystemParameters.WorkArea;
            // 夹到工作区内，避免窗口跑到已断开的屏幕外
            var newLeft = Math.Max(wa.Left, Math.Min(l, wa.Right - ActualWidth));
            var newTop = Math.Max(wa.Top, Math.Min(t, wa.Bottom - ActualHeight));
            Left = newLeft;
            Top = newTop;
        }
    }

    /// <summary>
    /// 把当前窗口位置/尺寸写入设置并持久化（防抖避免频繁写盘）。
    /// </summary>
    private void SaveWindowBounds()
    {
        _ = _settings.UpdateAsync(s =>
        {
            s.WindowLeft = Left;
            s.WindowTop = Top;
            s.WindowWidth = Width;
            s.WindowHeight = Height;
        });
    }

    /// <summary>
    /// 右下角缩放手柄拖动 → 调整窗口尺寸。
    /// </summary>
    private void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        var newWidth = Math.Max(MinWidth, Width + e.HorizontalChange);
        var newHeight = Math.Max(MinHeight, Height + e.VerticalChange);
        Width = newWidth;
        Height = newHeight;
    }

    /// <summary>
    /// 状态机变更：动画开始前触发，UI 层提前显示目标窗口让动画过程可见。
    /// 进入 CollapsedBall：先显示悬浮球，再触发收缩动画（主窗向球收缩）。
    /// 进入 Expanded：先恢复主窗位置/尺寸并显示，再触发膨胀动画（球向窗口膨胀）。
    /// </summary>
    private void OnWindowStateStateChanged(object? sender, WindowVisualState state)
    {
        Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case WindowVisualState.CollapsedBall:
                    // 先把球放到目标位置并显示，再触发收缩动画
                    ShowBallWindow();
                    // 主窗不立即隐藏，让收缩动画播放
                    break;
                case WindowVisualState.Expanded:
                    // 先恢复主窗并显示，再触发膨胀动画
                    RestoreFromBall();
                    break;
            }
        });
    }

    /// <summary>
    /// 动画完成后收尾：Collapse 后隐藏主窗，Expand 后隐藏球。
    /// </summary>
    private void OnWindowAnimationCompleted(object? sender, WindowVisualState state)
    {
        Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case WindowVisualState.CollapsedBall:
                    Hide();
                    break;
                case WindowVisualState.Expanded:
                    _ballWindow?.Hide();
                    // 修复:快速进入悬浮球再移出时,鼠标可能在主窗 Show() 之前已离开窗口区域,
                    // 此时 IsMouseOver=false,MouseLeave 永远不会触发,主窗会卡在展开状态。
                    // 在 Expand 完成 + 主窗已 Show 后,主动检查鼠标是否仍在窗口内,
                    // 若不在且未固定 + 在边缘,主动触发收缩。
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_windowState.Pin == PinState.Pinned) return;
                        if (!_windowState.IsAtEdge) return;
                        var localMouse = Mouse.GetPosition(this);
                        bool inside = localMouse.X >= 0 && localMouse.X <= ActualWidth
                                   && localMouse.Y >= 0 && localMouse.Y <= ActualHeight;
                        if (inside) return;
                        if (_windowState is Services.Windows.WindowStateService ws)
                        {
                            ws.OnMouseLeave();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                    break;
            }
        });
    }

    /// <summary>
    /// 从悬浮球恢复为窗口：恢复原位置与尺寸，并显示主窗（球先不隐藏，让膨胀动画覆盖）。
    /// </summary>
    private void RestoreFromBall()
    {
        var rect = _windowState.LastExpandedRect;
        if (rect.Width > 0 && rect.Height > 0)
        {
            Left = rect.Left;
            Top = rect.Top;
            Width = rect.Width;
            Height = rect.Height;
        }
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.ExpandedRect = new Rect(Left, Top, Width, Height);
        }
        Show();
    }

    private void ShowBallWindow()
    {
        if (_ballWindow == null)
        {
            _ballWindow = _services.GetRequiredService<BallWindow>();
        }
        var rect = ComputeBallRectAtEdge();
        _ballWindow.Left = rect.Left;
        _ballWindow.Top = rect.Top;
        _ballWindow.Width = rect.Width;
        _ballWindow.Height = rect.Height;
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.BallRect = rect;
            ae.BallView = _ballWindow.RootGrid;
        }
        _ballWindow.Show();
    }

    private void MainWindow_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_windowState is Services.Windows.WindowStateService ws)
        {
            ws.OnMouseLeave();
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        var newState = _windowState.Pin == PinState.Pinned ? PinState.Unpinned : PinState.Pinned;
        _windowState.SetPin(newState);
        PinButton.Opacity = newState == PinState.Pinned ? 1.0 : 0.5;
    }

    /// <summary>
    /// 关闭按钮：2 秒内再次点击则关闭应用，否则显示优雅浮动提示。
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if ((DateTime.Now - _lastCloseClickTime).TotalSeconds < 2)
        {
            HideCloseHint();
            Close();
            return;
        }
        _lastCloseClickTime = DateTime.Now;
        ShowCloseHint();
    }

    private void ShowCloseHint()
    {
        CloseHintBorder.Visibility = Visibility.Visible;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
        };
        CloseHintBorder.BeginAnimation(OpacityProperty, fadeIn);

        _closeHintTimer?.Stop();
        _closeHintTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _closeHintTimer.Tick += (_, _) =>
        {
            _closeHintTimer.Stop();
            HideCloseHint();
        };
        _closeHintTimer.Start();
    }

    private void HideCloseHint()
    {
        _closeHintTimer?.Stop();
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = CloseHintBorder.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
        };
        fadeOut.Completed += (_, _) => CloseHintBorder.Visibility = Visibility.Collapsed;
        CloseHintBorder.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// 单条记录删除按钮：直接删除（不需要二次确认）。
    /// </summary>
    private void DeleteRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            e.Handled = true;
            _ = _history.RemoveAsync(id);
        }
    }

    /// <summary>
    /// 清空全部记录按钮：2 秒内再次点击则真正清空，否则显示浮动提示。
    /// </summary>
    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if ((DateTime.Now - _lastClearClickTime).TotalSeconds < 2)
        {
            HideClearHint();
            _ = _history.ClearAsync();
            return;
        }
        _lastClearClickTime = DateTime.Now;
        ShowClearHint();
    }

    private void ShowClearHint()
    {
        ClearHintBorder.Visibility = Visibility.Visible;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
        };
        ClearHintBorder.BeginAnimation(OpacityProperty, fadeIn);

        _clearHintTimer?.Stop();
        _clearHintTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _clearHintTimer.Tick += (_, _) =>
        {
            _clearHintTimer.Stop();
            HideClearHint();
        };
        _clearHintTimer.Start();
    }

    private void HideClearHint()
    {
        _clearHintTimer?.Stop();
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = ClearHintBorder.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
        };
        fadeOut.Completed += (_, _) => ClearHintBorder.Visibility = Visibility.Collapsed;
        ClearHintBorder.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var win = _services.GetRequiredService<SettingsWindow>();
        win.Show();
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => Dispatcher.Invoke(RefreshHistory);

    private void OnSettingsChanged(object? sender, EventArgs e) => Dispatcher.Invoke(ApplySettingsToWindow);

    private void RefreshHistory()
    {
        HistoryList.Items.Clear();
        foreach (var item in _history.Items)
        {
            HistoryList.Items.Add(BuildItemContainer(item));
        }
    }

    private object BuildItemContainer(ClipboardRecord r)
    {
        // 行根容器：左侧类型色条 + 中间内容 + 右侧删除按钮
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

        // 左侧类型色条
        var accentBrush = r switch
        {
            TextRecord => new SolidColorBrush(Color.FromRgb(0x6C, 0x5C, 0xE7)),   // 紫色
            FileRecord => new SolidColorBrush(Color.FromRgb(0x00, 0xB8, 0xA9)),   // 青绿
            ImageRecord => new SolidColorBrush(Color.FromRgb(0xFF, 0x76, 0x75)),  // 珊瑚红
            _ => new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAA)),
        };
        var accentBar = new Border
        {
            Background = accentBrush,
            Margin = new Thickness(0, 6, 0, 6),
        };
        Grid.SetColumn(accentBar, 0);
        row.Children.Add(accentBar);

        // 右侧内容列
        var contentCol = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(8, 6, 8, 6),
        };

        switch (r)
        {
            case TextRecord t:
                var textBlock = new TextBlock
                {
                    Text = t.Text.Length > 60 ? t.Text[..60] + "..." : t.Text,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = Brushes.White,
                    FontSize = 13,
                };
                contentCol.Children.Add(textBlock);
                contentCol.ToolTip = new ToolTip { Content = t.Text };
                ToolTipService.SetInitialShowDelay(contentCol, 1000);
                ToolTipService.SetShowDuration(contentCol, 10000);
                break;

            case FileRecord f:
                var filePanel = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var path in f.FilePaths)
                {
                    var fileName = System.IO.Path.GetFileName(path);
                    filePanel.Children.Add(new TextBlock
                    {
                        Text = "📎 " + fileName,
                        Foreground = Brushes.White,
                        FontSize = 13,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                }
                contentCol.Children.Add(filePanel);
                break;

            case ImageRecord:
                contentCol.Children.Add(new TextBlock
                {
                    Text = "🖼 图片",
                    Foreground = Brushes.White,
                    FontSize = 13,
                });
                break;
        }

        // 底部来源信息行：左应用名，右时间
        var metaGrid = new Grid();
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var sourceBlock = new TextBlock
        {
            Text = r.SourceApp.Name,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 170)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 4, 8, 0),
        };
        Grid.SetColumn(sourceBlock, 0);
        metaGrid.Children.Add(sourceBlock);

        var timeBlock = new TextBlock
        {
            Text = r.CopiedAt.ToString("HH:mm:ss"),
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 170)),
            Margin = new Thickness(0, 4, 0, 0),
        };
        Grid.SetColumn(timeBlock, 1);
        metaGrid.Children.Add(timeBlock);

        contentCol.Children.Add(metaGrid);
        Grid.SetColumn(contentCol, 1);
        row.Children.Add(contentCol);

        // 右侧删除按钮（垂直居中，hover 变红）
        var deleteBtn = new Button
        {
            Content = "🗑",
            Width = 24,
            Height = 24,
            FontSize = 12,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xB0)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Tag = r.Id,
            ToolTip = "删除此记录",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        deleteBtn.MouseEnter += (_, _) => deleteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x76, 0x75));
        deleteBtn.MouseLeave += (_, _) => deleteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xB0));
        deleteBtn.Click += DeleteRecordButton_Click;
        Grid.SetColumn(deleteBtn, 2);
        row.Children.Add(deleteBtn);

        var item = new ListBoxItem { Content = row, Tag = r };
        return item;
    }

    private void HistoryList_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 点击的是删除按钮则跳过复制+重排（由 DeleteRecordButton_Click 处理）
        if (IsFromDeleteButton(e.OriginalSource))
        {
            return;
        }
        if (HistoryList.SelectedItem is ListBoxItem lbi && lbi.Tag is ClipboardRecord record)
        {
            _clipboardService.CopyToClipboard(record);
            _ = _history.ReorderToTopAsync(record.Id);
        }
    }

    private static bool IsFromDeleteButton(object source)
    {
        // 向上找视觉树，判断是否在 Button 中
        var fe = source as FrameworkElement;
        while (fe != null)
        {
            if (fe is Button) return true;
            fe = fe.Parent as FrameworkElement
                 ?? (fe.TemplatedParent as FrameworkElement);
        }
        return false;
    }

    private void ApplySettingsToWindow()
    {
        var s = _settings.Current;
        // 用背景 brush 的 Opacity 控制透明度，避免与 ScaleFade 动画的 RootBorder.Opacity 冲突
        // （Storyboard FillBehavior=HoldEnd 会锁死元素 Opacity，导致滑块失效）
        RootBackgroundBrush.Opacity = s.Opacity;
        if (_animationEngine is Animations.AnimationEngine ae)
        {
            ae.SetStrategy(s.AnimationKey);
        }
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                _backdrop.Apply(hwnd, s.Backdrop, s.Opacity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reapply backdrop failed");
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _history.ItemsChanged -= OnHistoryChanged;
        _settings.Changed -= OnSettingsChanged;
        _windowState.StateChanged -= OnWindowStateStateChanged;
        _windowState.AnimationCompleted -= OnWindowAnimationCompleted;
        _snapSuppressor?.Dispose();
        base.OnClosing(e);
    }
}
