# Hsp Copier

> Windows 桌面剪贴板历史小组件 - 可拖动置顶小窗口 + 边缘吸附变形为悬浮球 + 4 种炫酷变形动画。

## 特性

- 可拖动、可缩放的置顶圆角小窗口
- 位于屏幕边缘 + 鼠标离开 → 变形为悬浮球（带炫酷动画）
- 鼠标移到悬浮球 → 变形回窗口主体
- 固定按钮强制不变形
- 剪贴板历史：文本 / 文件 / 图片
- 长文本悬停 1s 预览全部
- 点击记录复制、可配置是否重排
- 4 种变形动画：缩放渐变 / 液体吸附 / 3D 翻转折叠 / 粒子消散重组
- 毛玻璃背景，可调透明度
- 内置 + 本地自定义背景图、悬浮球 GIF
- 系统托盘
- 记录复制来源应用
- GitHub 在线更新（Velopack fast-restart）

## 技术栈

- C# WPF .NET 8 self-contained single-file
- SkiaSharp (粒子/液体动画)
- Viewport3D (3D 翻转)
- Velopack (在线更新)
- Inno Setup (安装包)

## 开发

```bash
# 还原
dotnet restore HspCopier.sln

# 构建
dotnet build HspCopier.sln -c Debug

# 运行
dotnet run --project src/HspCopier.App

# 测试
dotnet test
```

## 发布

```powershell
# 1. 单文件打包
.\tools\publish.ps1 -Version 0.1.0

# 2. Velopack 更新包
.\tools\velopack-pack.ps1 -Version 0.1.0

# 3. Inno Setup 安装包
iscc installer\hsp-copier.iss
```

CI 会自动在打 tag 时构建并发布 GitHub Release。

## 数据位置

`%APPDATA%\HspCopier\`
- `config.json` - 用户设置
- `history.json` - 剪贴板历史
- `images/` - 图片记录
- `backgrounds/` - 自定义背景图
- `balls/` - 自定义悬浮球图
- `logs/` - 日志

## 关于"在线更新不退出"

.NET single-exe 运行时主程序被 OS 文件锁定，严格意义的零中断热更新不可行。本项目采用：
- **主程序**：Velopack fast-restart（~1s 快速重启 + 状态恢复）
- **扩展点**（动画策略/主题，v2）：插件化 AssemblyLoadContext 热加载/卸载，真正零中断

详见 `plan.md` §4.8。
