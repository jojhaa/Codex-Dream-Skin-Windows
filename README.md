# Codex Dream Skin Windows

[English](./README.en.md) · [v0.3.4 发布说明](./docs/releases/v0.3.4.md) · [更新日志](./CHANGELOG.md)

面向 Windows 版 Codex Desktop 的非官方可视化主题管理器。它通过本机回环 CDP 将图片、玻璃材质和区域构图应用到 Codex，不修改 `WindowsApps`、Codex 二进制文件或 `app.asar`。

> 当前公开版本：`v0.3.4`
>
> 支持平台：Windows 10 1809 及以上、Windows 11
>
> 项目仓库：[`jojhaa/Codex-Dream-Skin-Windows`](https://github.com/jojhaa/Codex-Dream-Skin-Windows)

## 下载绿色版 EXE（推荐）

无需安装 .NET SDK，也不需要先克隆仓库。

**[下载 CodexDreamSkin-Windows-x64-v0.3.4.zip](https://github.com/jojhaa/Codex-Dream-Skin-Windows/releases/download/v0.3.4/CodexDreamSkin-Windows-x64-v0.3.4.zip)**

[查看 v0.3.4 发布页面与中英文版本日志](https://github.com/jojhaa/Codex-Dream-Skin-Windows/releases/tag/v0.3.4)

SHA-256：

```text
AA43F6BF0A9F857C50534294AFBAC57D5BCC7B559F188BA6AEC98B5A175D17B9
```

使用步骤：

1. 下载 ZIP 并完整解压到普通文件夹。
2. 保留压缩包内的目录结构，不要只复制单个 EXE。
3. 双击 `CodexDreamSkin.exe`。
4. 在主题管理器中选择或编辑主题，然后点击“正式应用”。
5. 如需普通启动 Codex 后自动套用主题，可在管理器中启用实时同步与自动接管。

> 当前绿色版尚未进行代码签名。首次运行可能出现 Windows 安全提示，请先确认下载地址来自本仓库，并核对上方 SHA-256。签名完成前请勿从第三方收费、捆绑或转载渠道下载。

## 主要功能

- 原生 WinUI 3 主题管理器，包含概览、主题、诊断和设置页面。
- 主背景、左侧栏、任务输入框、首页照片框、首页输入框、拍立得六个独立图片区域。
- 焦点 X/Y、缩放、填充方式、水平/垂直偏移和真实 Codex 比例取景。
- 浅色/深色即时预览、自动配色、肤色规避、对比度提醒和可视化颜色选择。
- 消息、摘要、任务预览、菜单、工作区、代码/差异、首页建议等组件的独立玻璃材质。
- 文件、编辑、视图、帮助四组 Codex 应用菜单中文翻译，并为菜单弹窗提供与主题一致的明暗模式样式。
- 主题库、导入/导出、历史记录、回滚和推荐构图恢复。
- 热重载，以及可选的“普通启动 Codex 后自动应用主题”接管模式。
- 原生 Windows 托盘常驻：关闭窗口转入后台，双击恢复；右键可打开管理器、主题、诊断、设置，也可隐藏或彻底退出。
- 设置页提供“开机自启动”，应用包使用 Windows 原生启动任务，便携 EXE 使用当前用户启动项。
- 设置页可手动检查唯一官方 GitHub 仓库的最新正式版本，并持续展示永久免费、退款提醒和官方项目地址。
- 自动发现 Microsoft Store 更新后的 Codex 安装路径，不绑定单一版本目录。
- 本机 CDP 端口诊断、占用进程识别和受保护的关闭操作。
- Kanna Blue 作为内置示例主题，覆盖首页、任务、设置、插件、站点、拉取请求、聊天、终端、审查面板、菜单和悬停抽屉。

## 界面展示

### 主题管理器

<table>
  <tr>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-preview.png" alt="主题管理器真实比例即时预览" width="100%">
      <br>
      <sub>真实比例即时预览</sub>
    </td>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-materials.png" alt="主题管理器明暗玻璃材质编辑" width="100%">
      <br>
      <sub>浅色与深色玻璃材质</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-components.png" alt="主题管理器组件级高级材质编辑" width="100%">
      <br>
      <sub>组件级高级材质</sub>
    </td>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-composition.png" alt="主题管理器独立区域构图编辑" width="100%">
      <br>
      <sub>独立区域构图与取景</sub>
    </td>
  </tr>
</table>

### Codex 明暗模式

<table>
  <tr>
    <td width="50%" align="center">
      <img src="./docs/images/readme/codex-light.png" alt="Kanna Blue Codex 浅色模式" width="100%">
      <br>
      <sub>浅色模式</sub>
    </td>
    <td width="50%" align="center">
      <img src="./docs/images/readme/codex-dark.png" alt="Kanna Blue Codex 深色模式" width="100%">
      <br>
      <sub>深色模式</sub>
    </td>
  </tr>
</table>

### 设置页面

![Kanna Blue 主题化 Codex 设置页面](./docs/images/readme/codex-settings.png)

### 应用菜单翻译

<p align="center">
  <img src="./docs/images/readme/codex-menu-translation.png" alt="Codex 视图菜单中文翻译" width="420">
  <br>
  <sub>文件、编辑、视图、帮助菜单中文化并保持主题材质</sub>
</p>

## 从源码运行与高级用法

### 1. 使用脚本安装并启动主题引擎

```powershell
git clone https://github.com/jojhaa/Codex-Dream-Skin-Windows.git
cd Codex-Dream-Skin-Windows\windows
powershell -ExecutionPolicy RemoteSigned -File .\scripts\install-dream-skin.ps1
powershell -ExecutionPolicy RemoteSigned -File .\scripts\start-dream-skin.ps1
```

### 2. 从源码运行 WinUI 主题管理器

需要安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

```powershell
cd Codex-Dream-Skin-Windows\windows
dotnet run --project .\app\CodexDreamSkin\CodexDreamSkin.csproj -c Release -p:Platform=x64
```

管理器启动后可以导入图片、调整每个区域的取景参数、实时预览明暗模式并应用主题。

### 3. 自行生成 x64 绿色版

```powershell
cd Codex-Dream-Skin-Windows\windows
dotnet publish .\app\CodexDreamSkin\CodexDreamSkin.csproj `
  -c Release `
  -p:Platform=x64 `
  -r win-x64 `
  -p:PortableExe=true `
  -o ..\release\CodexDreamSkin-win-x64
```

启动文件为 `release\CodexDreamSkin-win-x64\CodexDreamSkin.exe`。这是自包含 WinUI 3 发布目录；请保留 EXE 旁边的 DLL、PRI、资源和主题文件，不能只复制单个 EXE。

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
