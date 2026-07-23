# Changelog

## v0.3.2 — 2026-07-23

This release turns the Windows theme workflow into a native, visual theme studio while preserving the original external-injection safety boundary.

### Added

- Native WinUI 3 manager with Overview, Themes, Diagnostics, and Settings workspaces.
- Six independent image regions: page, sidebar, task composer, home hero, home composer, and Polaroid.
- Focus X/Y, zoom, fit, horizontal/vertical offset, actual-ratio viewport editing, copy-main, and recommended-composition recovery.
- Light/dark instant preview, automatic palette extraction, skin-tone avoidance, contrast warnings, and visual color controls.
- Component materials for messages, summaries, task previews, menus, workspace panels, code/diffs, and home suggestions.
- Theme package import/export, history, rollback, bounded retention, and safe staged-image cleanup.
- Continuous hot reload and optional background takeover for normally launched Codex.
- Dynamic Store package discovery, managed-port inspection, and guarded close controls.
- Chinese application menus for File, Edit, View, and Help.

### Improved

- Kanna Blue portrait glass across home, tasks, Settings, plugins, sites, pull requests, chat, terminal, review panels, menus, and hover drawers.
- Sidebar/project/task interaction, task preview readability, queued-message layout, composer image ownership, and narrow-window stability.
- Settings routing after edge-hover navigation, including restoration of the genuine native sidebar.
- Release serialization safety and compatibility with Codex Store version changes.

### Safety

- CDP remains bound to `127.0.0.1`.
- Codex binaries, WindowsApps, `.app`, and `app.asar` are not modified.
- No hidden PowerShell tray shortcut is installed.
- Package/process identity is revalidated before any guarded takeover or close operation.

### Validation

- Complete PowerShell regression suite passes.
- Renderer, payload, multi-image, manager-source, and application-menu tests pass.
- Windows x64 Release build completes with zero warnings and zero errors.
