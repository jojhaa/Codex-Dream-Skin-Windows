# Codex Dream Skin Windows

[English](./README.en.md) · [v0.3.2 发布说明](./docs/releases/v0.3.2.md) · [更新日志](./CHANGELOG.md)

面向 Windows 版 Codex Desktop 的非官方可视化主题管理器。它通过本机回环 CDP 将图片、玻璃材质和区域构图应用到 Codex，不修改 `WindowsApps`、Codex 二进制文件或 `app.asar`。

> 当前公开版本：`v0.3.2`
>
> 支持平台：Windows 10 1809 及以上、Windows 11
>
> 项目仓库：[`jojhaa/Codex-Dream-Skin-Windows`](https://github.com/jojhaa/Codex-Dream-Skin-Windows)

## 主要功能

- 原生 WinUI 3 主题管理器，包含概览、主题、诊断和设置页面。
- 主背景、左侧栏、任务输入框、首页照片框、首页输入框、拍立得六个独立图片区域。
- 焦点 X/Y、缩放、填充方式、水平/垂直偏移和真实 Codex 比例取景。
- 浅色/深色即时预览、自动配色、肤色规避、对比度提醒和可视化颜色选择。
- 消息、摘要、任务预览、菜单、工作区、代码/差异、首页建议等组件的独立玻璃材质。
- 主题库、导入/导出、历史记录、回滚和推荐构图恢复。
- 热重载，以及可选的“普通启动 Codex 后自动应用主题”接管模式。
- 自动发现 Microsoft Store 更新后的 Codex 安装路径，不绑定单一版本目录。
- 本机 CDP 端口诊断、占用进程识别和受保护的关闭操作。
- Kanna Blue 作为内置示例主题，覆盖首页、任务、设置、插件、站点、拉取请求、聊天、终端、审查面板、菜单和悬停抽屉。

## 快速开始

### 1. 安装并启动主题引擎

```powershell
git clone https://github.com/jojhaa/Codex-Dream-Skin-Windows.git
cd Codex-Dream-Skin-Windows\windows
powershell -ExecutionPolicy RemoteSigned -File .\scripts\install-dream-skin.ps1
powershell -ExecutionPolicy RemoteSigned -File .\scripts\start-dream-skin.ps1
```

### 2. 从源码运行主题管理器

需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

```powershell
cd Codex-Dream-Skin-Windows\windows
dotnet run --project .\app\CodexDreamSkin\CodexDreamSkin.csproj -c Release -p:Platform=x64
```

管理器启动后可以导入图片、调整每个区域的取景参数、实时预览明暗模式并应用主题。

## 常用命令

```powershell
# 验证安装、运行环境和当前主题
powershell -ExecutionPolicy RemoteSigned -File .\scripts\verify-dream-skin.ps1

# 恢复 Codex 原始外观
powershell -ExecutionPolicy RemoteSigned -File .\scripts\restore-dream-skin.ps1

# 运行完整 Windows 回归测试
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

## 工作原理与安全边界

1. 启动脚本动态查找当前 Microsoft Store Codex 包与真实进程路径。
2. Codex 通过仅绑定 `127.0.0.1` 的 Chromium 调试端口启动。
3. 注入器通过 CDP 向现有渲染页面加载主题 CSS、图片配置和运行时适配。
4. 热重载监听本地主题文件；修改后不需要重装 Codex。
5. 恢复脚本停止主题运行时并移除用户级状态，不改动官方应用文件。

本项目不会：

- 修改、替换或重新签名 Codex 安装包。
- 写入 `WindowsApps` 或解包 `app.asar`。
- 更改 API Base URL、API Key 或账户数据。
- 安装隐藏的 PowerShell 托盘快捷方式。
- 将 CDP 端口暴露到局域网。

启用 CDP 期间，请不要运行不受信任的本地程序。

## 项目结构

```text
windows/
├── app/CodexDreamSkin/    # WinUI 3 主题管理器
├── assets/                # 默认主题、CSS、注入载荷与示例图片
├── presets/               # 内置主题预设
├── scripts/               # 安装、启动、应用、验证与恢复脚本
├── tests/                 # PowerShell、Node.js 与实时界面回归测试
├── references/            # Windows 运行时与 QA 说明
└── SKILL.md               # Codex 自动化工作流
```

## 开发验证

```powershell
cd windows
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
dotnet build .\app\CodexDreamSkin\CodexDreamSkin.csproj -c Release -p:Platform=x64 -nologo
```

提交变更时请同步更新根目录的 [`DEVELOPMENT_PROGRESS.md`](./DEVELOPMENT_PROGRESS.md) 和 [`DEVELOPMENT_LOG.md`](./DEVELOPMENT_LOG.md)。

## 已知限制

- 主题依赖 Codex 当前的 Electron/Chromium DOM；Codex 大版本更新后可能需要更新选择器。
- “普通启动自动应用”属于可选用户级接管功能，关闭后普通 Codex 不会自动带主题。
- 实时界面测试要求本机 Codex 正在运行并已开启受管理的 CDP 会话。
- 本项目是非官方工具，与 OpenAI 无隶属或认可关系。

## 感谢与来源

感谢 [`Fei-Away/Codex-Dream-Skin`](https://github.com/Fei-Away/Codex-Dream-Skin) 提供最初的 Codex 外部主题思路与基础实现。本仓库在此基础上面向 Windows 进行了独立重构与持续维护。
