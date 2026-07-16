# Hsp Copier

> Windows 桌面剪贴板历史小组件 - 可拖动置顶小窗口 + 边缘吸附变形为悬浮球 + 4 种炫酷变形动画 + 动态悬浮球背景。

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
- 内置 + 本地自定义背景图
- **动态悬浮球背景**（64×64），两种风格可选：
  - **卡通形象**：深空霓虹配色圆脸 + 紫色发光，瞳孔随机看向上下左右，表情随机切换（平嘴 / 微笑 / 皱眉 / 惊讶 O 形），偶发眨眼
  - **天气动画**：每 30 秒自动检测网络 → IP 定位 → 调用 Open-Meteo 获取天气，9 种状态动画（晴 / 多云 / 雨 / 雪 / 雷 / 雾 / 夜间晴 / 无网 / 无定位），切换时 500ms CrossFade 平滑过渡
- 系统托盘
- 记录复制来源应用
- GitHub 在线更新（Velopack fast-restart）

## 技术栈

- C# WPF .NET 8 self-contained single-file
- SkiaSharp (粒子/液体动画)
- Viewport3D (3D 翻转)
- Velopack (在线更新)
- Inno Setup (安装包)
- ipinfo.io (IP 定位) + Open-Meteo (天气)

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

### 自动发版（CD）

仓库已配置 GitHub Actions CD（`.github/workflows/cd.yml`），打 tag 自动触发：

1. `dotnet publish` 单文件 exe
2. `vpk pack` 生成 `.nupkg` 全量/增量更新包 + `*-Setup.exe`
3. `iscc` 生成 Inno Setup 安装包（自动下载简体中文语言文件）
4. 上传至 GitHub Release

发版步骤：
```bash
git tag v0.1.1
git push origin v0.1.1
```

更新源：https://github.com/Huiyeji-Z/Hsp-Copier/releases/latest

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
