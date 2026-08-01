# Changelog

## Unreleased

暂无。

## v0.3.8 — 2026-08-01

### 中文版本日志（主要）

本次补丁恢复最新版 Codex Desktop 设置页、拉取请求详情页和 Ctrl+Alt+B 审查侧栏的主题透明度。

### 修复

- 恢复最新版 Codex 设置页的主题透明度：识别带搜索框与原生设置控件的新页面结构，并清除新增的 Electron 整页黑色底板。
- 恢复拉取请求等详情页的主题透明度：清除新版 `app-shell-detail-panel` 及其页面级父层底色。
- 恢复 Ctrl+Alt+B 审查侧栏的主题透明度：以稳定的 `data-app-shell-focus-area="right-panel"` 为边界，清除新版侧栏中的页面、滚动区、工具栏和粘性操作区纯色底板。
- 仅透明化页面尺寸的基础画布；设置卡片、输入框、消息和交互控件继续保留半透明玻璃材质。
- 将样式架构升级至 73，使已运行的 Codex 页面立即替换旧样式。

### 验证

- 真实 Codex 设置页确认整页基础画布为透明，5 个可读设置卡片仍保留玻璃渐变。
- 真实 Codex 拉取请求页确认外层画布、内容区和详情容器三层均为透明。
- 真实 Codex Ctrl+Alt+B 审查侧栏确认 6 个基础层均为透明，4 个快捷操作按钮仍保留轻微交互底色。
- 完整 Windows 回归通过，x64 Release 编译为 0 警告、0 错误。

### 双版本发布

- Portable 绿色版：`CodexDreamSkin-Windows-x64-v0.3.8-Portable.zip`
  - 大小：114,414,281 字节
  - SHA-256：`2B0FF33EE3BBEFE380F43B1113AA829224FAE2DFF8A9CFA270DB385782B2F6EB`
- Lite 轻量版：`CodexDreamSkin-Windows-x64-v0.3.8-Lite.zip`
  - 大小：15,953,316 字节
  - SHA-256：`A263AF0B93CCC906A2AA4CE4C63D4157151FA21ABD869B5F99E851C4A2E32F7B`

### English Summary

`v0.3.8` restores themed transparency across current Codex Settings, Pull Request details, and the Ctrl+Alt+B review sidebar while retaining readable materials for cards and controls.

## v0.3.7 — 2026-08-01

### 中文版本日志（主要）

本次补丁修复最新版 Codex Desktop 应用菜单仍显示英文命令的问题。

#### 修复

- 修复最新版 Codex 将应用菜单移到独立 `role="menubar"` 后，编辑菜单仍显示英文 `Undo / Redo / Cut / Copy / Paste / Delete / Select All` 的问题。
- 翻译器改为识别稳定的 `application-menu-trigger-*` 菜单按钮，不再依赖已移除的 `group/application-menu-top-bar` 标题栏类。
- 保留旧版 Codex 标题栏菜单兼容路径，并将样式架构升级至 72，确保运行中的旧监听器被替换。

#### 验证

- 真实 Codex 编辑菜单返回 `撤销、重做、剪切、复制、粘贴、删除、全选、设置…`。
- 文件、编辑、视图和帮助菜单分别包含 6、8、18、8 个中文命令，均无视口溢出。
- 完整 Windows 回归、x64 Release 编译以及 Portable/Lite 解压启动验证通过。

#### 双版本发布

- Portable 绿色版：`CodexDreamSkin-Windows-x64-v0.3.7-Portable.zip`
  - 大小：114,413,757 字节
  - SHA-256：`AA3675F56A5628440B402929334BDF8FB597BA24D1E22E92EA8F142612244CE5`
- Lite 轻量版：`CodexDreamSkin-Windows-x64-v0.3.7-Lite.zip`
  - 大小：15,952,868 字节
  - SHA-256：`2EAB2145FB2E95FDD6972E798294E9BF557700EFDE98B051CA9F0B5B0F4B86B4`

### English Summary

`v0.3.7` restores complete Chinese application-menu translation on current Codex Desktop builds.

- Recognizes the current standalone Radix-style menubar through narrow, stable `application-menu-trigger-*` identifiers.
- Removes the dependency on the deleted `group/application-menu-top-bar` class while retaining legacy compatibility.
- Verifies exact Chinese Edit labels and all four translated menu sizes against the real Codex renderer.
- Bumps the style schema to 72 so active pages replace stale menu listeners.

## v0.3.6 — 2026-08-01

### 中文版本日志（主要）

本次补丁恢复了最新版 Codex Desktop 的主题应用能力，并保留旧版页面结构兼容。

#### 修复

- 修复 Codex `26.727.6591.0` 更新页面结构后，主题引擎提示“未找到同时具有 Codex 主界面标记的 app:// 页面”的问题。
- 主页面识别同时支持旧版 `main.main-surface` 与最新版稳定的 `data-app-shell-*` 标记。
- 主题 CSS、实时预览截图、区域测量、原生早期注入和 Node 注入器同步兼容最新版主界面、标题栏、顶部渐隐和任务内容框架。
- 样式架构升级至 70，使已经运行的 Codex 页面能够替换旧的兼容样式。
- 保持 `avatar-overlay` 等辅助 `app://` 页面隔离，避免主题 CSS 误注入悬浮辅助窗口。

#### 验证

- 完整 Windows 回归测试、x64 Release 编译和 Portable/Lite 双版本发布验证通过。
- 真实 Codex `26.727.6591.0` 正式应用验证通过：主页面加载主题与路由样式，辅助页面不含主题类或主题样式表。
- 新增可执行的最新版主页面/辅助页面隔离测试，并更新现有实时主题、菜单、图层、消息和区域测量测试。

#### 双版本发布

- Portable 绿色版：`CodexDreamSkin-Windows-x64-v0.3.6-Portable.zip`
  - 无需预装 .NET 或 Windows App SDK Runtime。
  - 大小：114,413,732 字节
  - SHA-256：`5ECB6C8F448C8CBFFB12DDB4FFDB016C7AE5713D1405EF41E7935BEDFDC02233`
- Lite 轻量版：`CodexDreamSkin-Windows-x64-v0.3.6-Lite.zip`
  - 需要 .NET 10 Desktop Runtime x64 和 Windows App SDK Runtime 2.3 x64。
  - 大小：15,952,825 字节
  - SHA-256：`8A901471CCA33309EE1C77E0E0F907B4F2693E310AFA25D967BCB26CDC59C9DC`

### English Summary

`v0.3.6` restores theme application on current Codex Desktop builds while retaining compatibility with the legacy shell markup.

- Recognizes the stable `data-app-shell-*` markers introduced by Codex `26.727.6591.0`.
- Updates native and Node target probes, early injection, live capture, region measurement, renderer routing, and production CSS.
- Bumps the style schema to 70 so active pages replace stale compatibility CSS.
- Keeps auxiliary `app://` targets such as `avatar-overlay` unthemed.
- Adds live coverage proving that the real main page is themed and auxiliary pages remain isolated.

## v0.3.5 — 2026-07-27

### 中文版本日志（主要）

本次更新重构了整个 Windows 主题管理器，并将隔离预览、真实 Codex 画面回传、主题材质和 CDP 生命周期测试整合为一套完整工作流。

#### 新增

- 新增完整 Codex Desktop 隔离预览，覆盖窗口菜单、侧栏、主页、任务页、输入区和设置页。
- 预览器支持本地场景切换、模拟输入、模拟回复、设置交互和状态重置，不执行网络、文件或系统操作。
- 新增真实 Codex 画面回传；主题临时应用后通过受信任的本机 CDP 捕获完整画面，并按原始比例缩放到中间预览区。
- 新增“银河夜幕 · 星河玻璃”内置主题，包含星河背景、深色玻璃材质、连续背景和透明组件。
- 新增左栏连续主背景模式，以及“统一侧栏与工作区透明度”选项。
- 主题格式升级至 schema 11，支持装饰配置、侧栏背景来源和大区域透明度匹配。

#### 改进

- 将整个 Windows 管理器重构为单一扁平工作台，使用顶部品牌操作栏和连续工作区替代传统页面框架。
- 主题工作区采用主题库/编辑工具、Codex 预览和属性操作台三轨布局。
- 诊断、设置和概览同步改为满高平面分栏，并统一浅色、深色和高对比度资源。
- 真实预览使用固定画布与单一等比缩放层，避免背景和 Codex 界面产生比例漂移。
- 银河主题移除继承自人物主题的标题条、丝带和拍立得等装饰，只保留星河背景与功能玻璃层。
- 连续背景模式使用一个全窗口背景平面，左栏不再重复裁切区域图片。

#### 修复

- 修复 `CdpSession.SendAsync` 在序列化、发送、取消和断线路径中的 pending 请求残留。
- 新增真实 WebSocket 取消和断线行为测试，验证请求立即结束且 pending 字典归零。
- 修复完整隔离预览启动时的 MRT 本地化资源冲突闪退。
- 修复实时预览画面没有回传，以及预览比例缩放不准确的问题。
- 修复透明左栏仍残留材质、伪元素、边框或模糊的问题。

#### 双版本发布

- Portable 绿色版：`CodexDreamSkin-Windows-x64-v0.3.5-Portable.zip`
  - 无需预装 .NET 或 Windows App SDK Runtime。
  - 大小：110,987,055 字节
  - SHA-256：`3A018B7F8BE1E2938A430A3CDBAAD4338ECB291A861E3A615A071575FC07131D`
- Lite 轻量版：`CodexDreamSkin-Windows-x64-v0.3.5-Lite.zip`
  - 需要 .NET 10 Desktop Runtime x64 和 Windows App SDK Runtime 2.3 x64。
  - 大小：15,516,862 字节
  - SHA-256：`64FF47C04A43571B56E5C5D0073DECBE078C6C84E342C7E12251372BA498413A`
- Release 关闭 ReadyToRun，并将 Windows App SDK 改为按需组件引用；不再打包未使用的 AI/ML、ONNX、DirectML 和 Widgets。

### English Summary

`v0.3.5` rebuilds the Windows manager and unifies isolated interaction preview, real Codex frame capture, theme materials, and CDP lifecycle validation.

#### Highlights

- Flat Theme Studio workspace with theme/tool rail, complete Codex preview, and contextual inspector.
- Full local Codex Desktop fixture with Home, Task, Settings, composer interaction, deterministic replies, and no external side effects.
- Trusted loopback CDP frame capture with proportional whole-canvas scaling.
- New Milky Way Glass theme with a continuous full-window background and translucent functional surfaces.
- Continuous-sidebar mode plus an option to match sidebar, workspace, and header transparency.
- Schema 11 theme metadata and renderer 3.10.0.
- Correct pending-request cleanup across CDP serialization, send, cancellation, and disconnect paths, backed by executable WebSocket behavior tests.
- Added reproducible Portable and Lite release builds. Unused Windows App SDK AI/ML, ONNX, DirectML, and Widgets payloads are no longer distributed.

The Portable build remains self-contained; the Lite build requires the matching .NET 10 Desktop and Windows App SDK 2.3 x64 runtimes. Both builds are unsigned. They do not patch Codex binaries, `WindowsApps`, or `app.asar`; CDP remains restricted to the verified local loopback listener.

## v0.3.4 — 2026-07-24

### 中文版本日志（主要）

本次更新将 Windows 主题工作室完善为自包含绿色应用，并加强托盘生命周期、发布安全与 Codex 全界面主题覆盖。

#### 新增

- 新增适用于应用包和绿色版的原生 Windows 托盘，支持关闭转后台、双击恢复、Explorer 重启后恢复、键盘助记键、页面导航、隐藏和彻底退出。
- 新增开机自启动，分别使用应用包启动任务和绿色版当前用户启动项。
- 新增基于官方 GitHub Releases API 的手动版本检测。
- 新增永久免费、开源、退款、防二次售卖和唯一官方项目地址声明。
- 新增受管端口检测，以及仅针对已验证官方 Codex 进程的安全关闭操作。
- 新增透明背景应用图标，并同步到 EXE、管理器窗口、托盘、Store、开始菜单、磁贴和启动画面。

#### 改进

- 将公开项目介绍重构为 Windows 专用版本，增加产品截图布局和中英文文档。
- 扩展 Kanna Blue 对设置页、悬停抽屉、工具页、终端、审查侧栏、摘要、待发送消息及浅色/深色模式的覆盖。
- 优化主题管理器构图、预览、配色和材质编辑的响应式布局，并设置 770×680 最小窗口尺寸。
- 让绿色版存储、内置主题资源、本地化、诊断和设置页不再依赖应用包身份。
- 自动发现当前 Microsoft Store Codex 应用包，不再绑定单一安装版本。

#### 修复

- 修复绿色版进入诊断页和设置页时可能闪退的问题。
- 修复干净发布后内置主题图片丢失的问题。
- 托盘注册成功后才启用关闭转后台，避免产生无法访问的隐藏进程。
- 修复托盘菜单、页面导航、普通二次启动、隐藏、恢复和彻底退出之间的生命周期不同步。

#### 发布说明

- 主要下载文件为未签名、自包含的 Windows x64 绿色 ZIP。
- 绿色包继续保留公开主题人物素材。
- 代码签名流程暂缓。

### English Summary

`v0.3.4` turns the Windows theme studio into a self-contained green application with stronger lifecycle management, release safety, and visual coverage.

- Added native in-process notification-area support with close-to-background, double-click restore, Explorer recovery, localized navigation, hide, and full exit.
- Added packaged and portable Start with Windows support, manual release checks, authenticity information, managed-port diagnostics, and a transparent application icon.
- Improved responsive theme editing, portable storage, current Store-package discovery, bilingual documentation, and Kanna Blue coverage across Codex surfaces.
- Fixed portable Diagnostics/Settings crashes, missing bundled artwork, unsafe close-to-tray fallback, and tray/navigation lifecycle synchronization.
- The primary artifact is an unsigned, self-contained Windows x64 green ZIP with public theme artwork included; code signing remains deferred.

## v0.3.2 — 2026-07-23

This release turns the Windows theme workflow into a native, visual theme studio while preserving the original external-injection safety boundary.

### Added

- A native in-process Windows notification-area icon for packaged and portable builds, with close-to-tray, double-click restore, Explorer-restart recovery, and localized right-click commands for manager navigation, hiding, and full exit.
- Manual latest-release detection in Settings through GitHub's official Releases API, with no automatic download or execution.
- A persistent localized free-software/refund notice and canonical project/release links in Settings.
- A Start with Windows setting that now works in both packaged and portable deployments.
- Native WinUI 3 manager with Overview, Themes, Diagnostics, and Settings workspaces.
- Directly runnable, self-contained Windows x64 EXE publishing with package-identity-safe local storage.
- Six independent image regions: page, sidebar, task composer, home hero, home composer, and Polaroid.
- Focus X/Y, zoom, fit, horizontal/vertical offset, actual-ratio viewport editing, copy-main, and recommended-composition recovery.
- Light/dark instant preview, automatic palette extraction, skin-tone avoidance, contrast warnings, and visual color controls.
- Component materials for messages, summaries, task previews, menus, workspace panels, code/diffs, and home suggestions.
- Theme package import/export, history, rollback, bounded retention, and safe staged-image cleanup.
- Continuous hot reload and optional background takeover for normally launched Codex.
- Dynamic Store package discovery, managed-port inspection, and guarded close controls.
- Chinese application menus for File, Edit, View, and Help.

### Improved

- Replaced the template icon with a transparent-background Kanna Blue glass-window and photo-composition mark, shared by the EXE, live WinUI window, Store/Start assets, tiles, and portable output.
- Replaced the obsolete 70% migration card with a localized free-software/refund notice and a clickable canonical project link, backed by a compiled fallback and regression contract.
- Kanna Blue portrait glass across home, tasks, Settings, plugins, sites, pull requests, chat, terminal, review panels, menus, and hover drawers.
- Sidebar/project/task interaction, task preview readability, queued-message layout, composer image ownership, and narrow-window stability.
- Settings routing after edge-hover navigation, including restoration of the genuine native sidebar.
- Release serialization safety and compatibility with Codex Store version changes.

### Fixed

- Portable EXE navigation to Diagnostics and Settings now uses Windows App SDK MRT Core resources instead of a package-identity-only loader; diagnostics refresh failures are contained in-page instead of terminating the app.
- Portable publishing now copies the bundled `dream-reference.png` into `Assets\Theme`, so the built-in theme library opens with a valid image instead of reporting an invalid theme path.

### Safety

- CDP remains bound to `127.0.0.1`.
- Codex binaries, WindowsApps, and `app.asar` are not modified.
- No hidden PowerShell tray shortcut is installed.
- Package/process identity is revalidated before any guarded takeover or close operation.

### Validation

- Complete PowerShell regression suite passes.
- Renderer, payload, multi-image, manager-source, and application-menu tests pass.
- Windows x64 Release build completes with zero warnings and zero errors.
