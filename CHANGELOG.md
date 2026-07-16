# 变更日志

## 0.1.0 (2026-07-15)

### 新增
- 项目脚手架（5 层架构：Presentation / Application / Domain / SystemIntegration / Data）
- 主窗口透明置顶、圆角、自绘拖动/缩放
- 剪贴板监听（Win32 AddClipboardFormatListener + WM_CLIPBOARDUPDATE）
- 文本/文件记录解析
- 来源应用捕获（GetForegroundWindow → 进程路径 → FileVersionInfo）
- 历史记录服务（去重、上限、重排）
- 4 种变形动画策略：ScaleFade / LiquidAdsorption / FlipFold3D / ParticleDispersion
- 系统托盘（H.NotifyIcon）
- 设置窗口（背景模式、透明度、动画、记录数、性能模式）
- Velopack 集成 + GitHub Releases 更新
- Inno Setup 安装包脚本
- CI/CD GitHub Actions
