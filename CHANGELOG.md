# Changelog

## v0.3.4 — 2026-07-24

This release turns the Windows theme studio into a self-contained green application with stronger lifecycle management, release safety, and visual coverage.

### Added

- Native in-process Windows notification-area support for packaged and portable builds, including close-to-tray, double-click restore, Explorer-restart recovery, keyboard-accessible localized commands, and full exit.
- Start with Windows support for both packaged startup tasks and portable current-user startup entries.
- Manual latest-release checks against the official GitHub Releases API.
- A persistent free/open-source, refund, anti-resale, and canonical-project notice.
- Dynamic managed-port inspection and guarded closing of verified official Codex processes.
- A dedicated transparent application icon across the EXE, manager window, notification area, Store, Start, tiles, and splash assets.

### Improved

- Rebuilt the public introduction as a Windows-only project with an organized product gallery and Chinese/English documentation.
- Extended Kanna Blue coverage across Codex Settings, hover drawers, utility pages, terminal, review sidebar, summaries, queued messages, and light/dark modes.
- Added responsive theme-manager composition, preview, color, and material layouts with a 770×680 minimum window contract.
- Made portable storage, bundled theme assets, localization resources, diagnostics, and Settings independent of package identity.
- Automatically discovers the current Microsoft Store Codex package instead of binding to one installed version.

### Fixed

- Prevented Diagnostics and Settings navigation from terminating the unpackaged portable manager.
- Restored the bundled theme image in clean portable publishes.
- Prevented close-to-tray from activating unless Windows successfully registers the notification icon.
- Kept the tray menu, page navigation, normal second launch, hide, restore, and full-exit lifecycle synchronized.

### Distribution

- The primary downloadable artifact is the unsigned, self-contained Windows x64 green ZIP.
- Public theme artwork remains included.
- Code signing is intentionally deferred.

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
