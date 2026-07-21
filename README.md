# Hsp Copier

> Windows 桌面剪贴板历史小组件 - 可拖动置顶小窗口 + 边缘吸附变形为悬浮球 + 动态悬浮球背景。

## 特性

- 可拖动、可缩放的置顶圆角小窗口
- 主窗贴左/右屏幕边缘 + 鼠标离开 → 自动变形为悬浮球
- 鼠标移入悬浮球 → 变形回窗口主体
- 📌 固定按钮强制不变形
- 剪贴板历史：文本 / 文件 / 图片（图片记录功能开发中）
- 长文本悬停 1 秒预览全部
- 点击记录立即复制 + 自动重排到列表顶部
- 变形动画：缩放渐变（500ms 平滑过渡）
- 毛玻璃背景，透明度可调
- **动态悬浮球背景**（64×64），两种风格可选：
  - **卡通形象**：深空霓虹配色圆脸 + 紫色发光，瞳孔随机看向上下左右，表情随机切换（平嘴 / 微笑 / 皱眉 / 惊讶 O 形），偶发眨眼
  - **天气动画**：每 30 秒自动检测网络 → IP 定位 → 调用 Open-Meteo 获取天气，9 种状态动画（晴 / 多云 / 雨 / 雪 / 雷 / 雾 / 夜间晴 / 无网 / 无定位），切换时 500ms CrossFade 平滑过渡
- 系统托盘（双击显示窗口）
- 自动记录复制来源应用
- GitHub 在线更新（Velopack fast-restart）

## 安装

### 下载安装包（推荐）

1. 前往 [Releases 页面](https://github.com/Huiyeji-Z/Hsp-Copier/releases/latest)
2. 下载 `HspCopier-<版本>-Setup.exe`（Velopack 安装包）
3. 双击运行，按向导完成安装（per-user 安装，无需管理员权限）
4. 安装目录：`%LOCALAPPDATA%\HspCopier\`
5. 在线更新功能依赖此安装方式（其他安装方式 `IsInstalled=false` 会跳过更新检查）

> ⚠️ 不要用 Inno Setup 旧版安装包安装，那样 Velopack 不会注册安装元数据，无法在线更新。

## 使用说明

### 主窗口

| 元素 | 操作 | 效果 |
|---|---|---|
| 标题栏 / 窗口空白 | 鼠标左键拖动 | 移动窗口（限制在屏幕工作区内） |
| 右下角手柄 | 拖动 | 调整窗口大小 |
| 📌 固定按钮 | 点击 | 切换固定状态。固定后即使鼠标离开边缘也不会收缩为球 |
| ⚙ 设置按钮 | 点击 | 打开设置窗口 |
| ✕ 关闭按钮 | 2 秒内连续点击两次 | 关闭应用。首次点击会从底部弹出"再次点击 ✕ 关闭应用"提示 |
| 历史项 | 单击 | 复制该条记录到剪贴板 + 重排到列表顶部 |
| 文本项 | 鼠标悬停 1 秒 | 弹出 ToolTip 显示完整文本（10 秒后自动消失） |

### 历史记录视觉

每条记录左侧有 3px 色条标识类型：

| 类型 | 色条颜色 | 显示 |
|---|---|---|
| 文本 | 紫色 `#6C5CE7` | 文本内容（超 60 字符截断为 `...`） |
| 文件 | 青绿 `#00B8A9` | `📎 文件名`（多文件多行显示） |
| 图片 | 珊瑚红 `#FF7675` | `🖼 图片` |

每条记录底部显示：
- 左下：复制来源应用名（如 `chrome.exe` / `WeChat`）
- 右下：复制时间 `HH:mm:ss`

### 悬浮球变形规则

**收缩为球**：满足以下全部条件
1. 主窗口处于展开状态
2. 主窗口贴在屏幕**左**或**右**边缘（容差 8px，上下边缘不触发）
3. 鼠标移出主窗口区域
4. 未点击 📌 固定按钮

**展开为窗口**：鼠标移入悬浮球区域

### 悬浮球风格

#### 卡通形象（默认，零联网）

深紫蓝径向渐变圆脸，紫色发光晕，银白大眼 + 紫色瞳孔 + 霓虹粉嘴。

- 瞳孔每 3-5 秒随机看向 上 / 下 / 左 / 右 之一（1.2 秒平滑过渡）
- 嘴部每 5-10 秒切换表情：平嘴 / 微笑 / 皱眉 / 惊讶 O 形
- 每次切表情有 10% 概率触发眨眼（300ms，眼睛 Y 轴缩放 1→0.1→1）

#### 天气动画

启用后立即触发一次刷新，之后每 30 秒自动刷新。

**检测流程**：
1. 检查网卡可用性（`NetworkInterface.GetIsNetworkAvailable()`）
2. 调用 `ipinfo.io` 反查坐标（6 小时缓存，避免重复请求）
3. 调用 `open-meteo.com` 获取 `weather_code` + `is_day`
4. 映射到 9 种状态之一

**降级机制**：
- HTTP 请求超时 4 秒
- 连续 3 次失败（无网 / 无定位 / 未知）后自动切到 5 分钟节流模式
- 恢复正常后自动回到 30 秒刷新

**状态动画**：

| 状态 | 含义 | 视觉 |
|---|---|---|
| Sunny | 白天晴天 | 黄色太阳 + 8 道光线旋转 |
| Cloudy | 多云 / 阴天 | 白色云朵漂移 |
| Rainy | 下雨 | 灰云 + 蓝色雨线下落 |
| Snowy | 下雪 | 灰云 + 白色雪花飘落 |
| Thunder | 雷暴 | 深灰云 + 闪电闪烁 |
| Foggy | 雾 | 灰色半透明带横向漂移 |
| NightClear | 夜间晴 | 深蓝背景 + 月牙 + 星星闪烁 |
| NoNetwork | 无网络 | 红色 WiFi 图标呼吸 |
| NoLocation | 有网但定位失败 | 红色定位针上下浮动 |

天气状态切换时使用 500ms CrossFade 平滑过渡。

### 设置

打开方式：主窗口 → ⚙ 设置按钮

| 区段 | 配置项 | 可选值 | 默认值 | 说明 |
|---|---|---|---|---|
| 外观 | 背景模式 | 毛玻璃 (Acrylic) | Acrylic | 当前仅支持毛玻璃 |
| 外观 | 背景透明度 | 0.20 – 1.00，步进 0.05 | 0.85 | 拖动时实时预览，左侧"浅"、右侧"深" |
| 动画 | 变形动画 | 缩放渐变 | ScaleFade | 主窗 ↔ 球 变形动画 |
| 悬浮球 | 动画风格 | 卡通形象 / 天气动画 | 卡通形象 | 切换至天气动画时立即触发刷新并启动 30 秒定时器 |
| 剪贴板 | 最大记录数 | 5 – 50 | 10 | 超出自动移除最旧记录 |

### 剪贴板历史

#### 记录类型

| 类型 | 说明 | 去重 |
|---|---|---|
| 文本 | 任意纯文本 | SHA256(text) 去重 |
| 文件 | 支持多文件批量复制 | SHA256(排序后的"路径\|大小\修改时间") |
| 图片 | 落盘到 `images/` 目录 | SHA256(图片字节) |

> 图片记录功能开发中，当前版本仅支持文本与文件。

#### 去重与防抖

- 相同内容的记录再次复制时，**不会新增**，而是把已有条目重排到顶部
- 100ms 防抖窗口，避免系统重复复制产生多条记录

#### 来源应用识别

通过 `GetForegroundWindow → GetWindowThreadProcessId → QueryFullProcessImageName` 链解析当前焦点窗口的进程信息，优先使用 FileDescription，其次 ProductName，最后回退到文件名。

### 系统托盘

应用启动后在系统托盘显示 Hsp Copier 图标。

| 操作 | 效果 |
|---|---|
| 双击托盘图标 | 显示主窗口 |
| 右键 → 显示窗口 | 显示主窗口 |
| 右键 → 切换固定 | 切换 Pin 状态 |
| 右键 → 设置... | 打开设置窗口 |
| 右键 → 检查更新 | 检查 GitHub 最新版本 |
| 右键 → 退出 | 退出应用 |

### 在线更新

**更新源**：https://github.com/Huiyeji-Z/Hsp-Copier/releases/latest

**操作流程**：
1. 打开设置 → 点击底部 `检查更新` 按钮
2. 按钮文本变为 `检查中...`，禁用
3. 检测到新版本 → 弹窗显示版本号 + Release Notes → 询问"是否立即下载并应用？"
4. 点"是" → 按钮显示 `下载中... {百分比}%` 实时进度
5. 下载完成 → 应用 Velopack fast-restart（约 1 秒快速重启 + 状态恢复）

> 仅在通过 Velopack 安装包安装的版本下可用。开发模式（直接 `dotnet run`）下 `IsInstalled=false`，自动跳过更新检查。

## 技术栈

- C# WPF .NET 8 self-contained single-file
- Velopack（在线更新 + 安装包）
- H.NotifyIcon（系统托盘）
- ipinfo.io（IP 定位）+ Open-Meteo（天气）
- Serilog（日志）

## 开发

```bash
# 还原
dotnet restore HspCopier.sln

# 构建
dotnet build HspCopier.sln -c Debug

# 运行（开发模式，更新检查自动跳过）
dotnet run --project src/HspCopier.App

# 测试
dotnet test
```

### 项目结构

```
src/
├── HspCopier.Core/          # 实体 / 接口 / 设置 (无 UI 依赖)
├── HspCopier.Services/       # 业务服务 (剪贴板 / 历史 / 天气 / 托盘 / 更新)
├── HspCopier.Win32/          # P/Invoke (剪贴板监听 / 窗口钩子 / DWM)
├── HspCopier.Animations/     # 变形动画策略
└── HspCopier.App/            # WPF 主程序 (MainWindow / BallWindow / SettingsWindow / UserControls)
```

## 发布

### 本地手动打包

```powershell
# 1. 单文件打包（dotnet publish self-contained single-file）
.\tools\publish.ps1 -Version 0.1.0

# 2. Velopack 更新包 + 安装包（生成 .nupkg 全量/增量包 + Setup.exe）
.\tools\velopack-pack.ps1 -Version 0.1.0
```

### 自动发版（CD）

仓库已配置 GitHub Actions CD（`.github/workflows/cd.yml`），打 tag 自动触发：

1. `dotnet publish` 单文件 exe
2. `vpk pack` 生成 `.nupkg` 全量/增量更新包 + `*-Setup.exe`
3. 重命名 Setup.exe 为 `HspCopier-<版本>-Setup.exe`
4. 上传至 GitHub Release：`HspCopier-*-Setup.exe` + `*.nupkg` + `releases.*.json`

发版步骤：
```bash
git tag v0.1.1
git push origin v0.1.1
```

## 数据存储位置

根目录：`%APPDATA%\HspCopier\`

| 文件 / 目录 | 作用 |
|---|---|
| `config.json` | 用户设置（JSON 缩进格式） |
| `history.json` | 剪贴板历史（多态 `$type` 字段标识类型） |
| `images/` | 图片记录（按 SHA256 命名，去重存储） |
| `backgrounds/` | 预留：自定义窗口背景图 |
| `balls/` | 预留：自定义悬浮球图 |
| `logs/hspcopier.log` | Serilog 日志 |

### 备份 / 迁移

1. 退出 Hsp Copier
2. 复制整个 `%APPDATA%\HspCopier\` 目录到新机器的同路径
3. 重新启动应用即恢复设置 + 历史 + 图片

### 原子写入

`config.json` 与 `history.json` 都采用"先写 .tmp → File.Replace"模式，断电或崩溃不会留下半截文件。

## 关于"在线更新不退出"

.NET single-exe 运行时主程序被 OS 文件锁定，严格意义的零中断热更新不可行。本项目采用：
- **主程序**：Velopack fast-restart（约 1 秒快速重启 + 状态恢复）
- **扩展点**（动画策略 / 主题，v2 规划）：插件化 AssemblyLoadContext 热加载/卸载，真正零中断

## 已知限制 / 开发中

以下功能在代码中已部分预留或正在开发，当前版本暂未完全可用：

- **变形动画**：当前仅实现"缩放渐变"，"液体吸附 / 3D 翻转折叠 / 粒子消散重组"为 v2 规划
- **图片剪贴板记录**：监听器分支未实现，当前版本仅记录文本与文件
- **托盘右键菜单**：菜单项已定义，事件接线开发中（双击托盘显示窗口已可用）
- **开机自启**：设置字段已预留，注册表写入逻辑开发中
- **单条删除 / 清空 / 收藏 / 搜索历史**：服务接口已实现，UI 入口开发中
- **自定义背景图 / 自定义悬浮球图**：目录已创建，选择 UI 开发中
