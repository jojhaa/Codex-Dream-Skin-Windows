# Changelog

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
