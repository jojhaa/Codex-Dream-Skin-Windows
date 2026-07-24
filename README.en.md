# Codex Dream Skin Windows

[中文](./README.md) · [v0.3.4 release notes](./docs/releases/v0.3.4.md) · [Changelog](./CHANGELOG.md)

An unofficial visual theme manager for Codex Desktop on Windows. It applies artwork, glass materials, and per-region composition through loopback CDP without modifying `WindowsApps`, Codex binaries, or `app.asar`.

> Current public release: `v0.3.4`
>
> Supported platform: Windows 10 version 1809 or later, and Windows 11
>
> Repository: [`jojhaa/Codex-Dream-Skin-Windows`](https://github.com/jojhaa/Codex-Dream-Skin-Windows)

## Download the portable EXE package (recommended)

No .NET SDK or repository clone is required.

**[Download CodexDreamSkin-Windows-x64-v0.3.4.zip](https://github.com/jojhaa/Codex-Dream-Skin-Windows/releases/download/v0.3.4/CodexDreamSkin-Windows-x64-v0.3.4.zip)**

[View the v0.3.4 Release and bilingual release notes](https://github.com/jojhaa/Codex-Dream-Skin-Windows/releases/tag/v0.3.4)

SHA-256:

```text
AA43F6BF0A9F857C50534294AFBAC57D5BCC7B559F188BA6AEC98B5A175D17B9
```

Steps:

1. Download the ZIP and extract the complete archive to a normal folder.
2. Keep the included directory structure; do not copy only the EXE.
3. Run `CodexDreamSkin.exe`.
4. Select or edit a theme in the manager, then choose the final apply action.
5. To reapply the theme after an ordinary Codex launch, enable live synchronization and automatic takeover in the manager.

> The current portable build is unsigned. Windows may show a security prompt on first launch. Confirm that the download came from this repository and verify the SHA-256 above. Until signing is available, do not use paid, bundled, or third-party redistribution channels.

## Highlights

- Native WinUI 3 manager with Overview, Themes, Diagnostics, and Settings pages.
- Six independent artwork regions: page, sidebar, task composer, home hero, home composer, and Polaroid.
- Focus X/Y, zoom, fit mode, horizontal/vertical offsets, and viewports based on real Codex proportions.
- Instant light/dark preview, automatic palette extraction, skin-tone avoidance, contrast warnings, and visual color controls.
- Independent glass materials for messages, summaries, task previews, menus, workspace panels, code/diffs, and home suggestions.
- Chinese localization for the Codex File, Edit, View, and Help application menus, with theme-aware light and dark popup styling.
- Theme library, import/export, history, rollback, and recommended-composition recovery.
- Hot reload and an optional takeover mode that reapplies the theme after a normal Codex launch.
- Native Windows notification-area support: close to background, double-click to restore, or right-click to open the manager, Themes, Diagnostics, Settings, hide the window, or exit.
- A Start with Windows setting backed by a native startup task for packaged installs and a current-user startup entry for the portable EXE.
- Manual latest-release checks against the only official GitHub repository, alongside a persistent free-software, refund, and canonical-project notice in Settings.
- Dynamic Microsoft Store package discovery instead of a hard-coded Codex version path.
- Loopback CDP diagnostics, owning-process inspection, and guarded process controls.
- The bundled Kanna Blue example covers Home, tasks, Settings, plugins, sites, pull requests, chat, terminal, review panels, menus, and hover drawers.

## Gallery

### Theme manager

<table>
  <tr>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-preview.png" alt="True-ratio instant preview in the theme manager" width="100%">
      <br>
      <sub>True-ratio instant preview</sub>
    </td>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-materials.png" alt="Light and dark glass material editing" width="100%">
      <br>
      <sub>Light and dark glass materials</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-components.png" alt="Per-component advanced material editing" width="100%">
      <br>
      <sub>Per-component advanced materials</sub>
    </td>
    <td width="50%" align="center">
      <img src="./docs/images/readme/theme-manager-composition.png" alt="Independent region composition editing" width="100%">
      <br>
      <sub>Independent region composition</sub>
    </td>
  </tr>
</table>

### Codex light and dark modes

<table>
  <tr>
    <td width="50%" align="center">
      <img src="./docs/images/readme/codex-light.png" alt="Kanna Blue Codex light mode" width="100%">
      <br>
      <sub>Light mode</sub>
    </td>
    <td width="50%" align="center">
      <img src="./docs/images/readme/codex-dark.png" alt="Kanna Blue Codex dark mode" width="100%">
      <br>
      <sub>Dark mode</sub>
    </td>
  </tr>
</table>

### Settings

![Kanna Blue themed Codex Settings page](./docs/images/readme/codex-settings.png)

### Translated application menus

<p align="center">
  <img src="./docs/images/readme/codex-menu-translation.png" alt="Chinese translation of the Codex View menu" width="420">
  <br>
  <sub>Chinese File, Edit, View, and Help menus with theme-aware materials</sub>
</p>

## Source and advanced usage

### 1. Install and start the theme engine with scripts

```powershell
git clone https://github.com/jojhaa/Codex-Dream-Skin-Windows.git
cd Codex-Dream-Skin-Windows\windows
powershell -ExecutionPolicy RemoteSigned -File .\scripts\install-dream-skin.ps1
powershell -ExecutionPolicy RemoteSigned -File .\scripts\start-dream-skin.ps1
```

### 2. Run the WinUI manager from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) first.

```powershell
cd Codex-Dream-Skin-Windows\windows
dotnet run --project .\app\CodexDreamSkin\CodexDreamSkin.csproj -c Release -p:Platform=x64
```

Use the manager to import artwork, compose each region, preview light and dark modes, and apply the theme.

### 3. Build your own x64 portable package

```powershell
cd Codex-Dream-Skin-Windows\windows
dotnet publish .\app\CodexDreamSkin\CodexDreamSkin.csproj `
  -c Release `
  -p:Platform=x64 `
  -r win-x64 `
  -p:PortableExe=true `
  -o ..\release\CodexDreamSkin-win-x64
```

Launch `release\CodexDreamSkin-win-x64\CodexDreamSkin.exe`. This is a self-contained WinUI 3 deployment directory; keep the DLL, PRI, resource, and theme files beside the EXE instead of copying the EXE alone.

## Common commands

```powershell
# Verify installation, runtime state, and the active theme
powershell -ExecutionPolicy RemoteSigned -File .\scripts\verify-dream-skin.ps1

# Restore the original Codex appearance
powershell -ExecutionPolicy RemoteSigned -File .\scripts\restore-dream-skin.ps1

# Run the complete Windows regression suite
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

## How it works and safety boundary

1. The launcher dynamically discovers the current Microsoft Store Codex package and real process path.
2. Codex starts with a Chromium debugging endpoint bound only to `127.0.0.1`.
3. The injector uses CDP to load theme CSS, artwork configuration, and runtime adapters into existing renderer pages.
4. Hot reload watches local theme files, so changing a draft does not require reinstalling Codex.
5. The restore script stops the theme runtime and removes user-level state without changing official application files.

This project does not:

- Patch, replace, or re-sign the Codex installation.
- Write to `WindowsApps` or unpack `app.asar`.
- Change API Base URLs, API keys, or account data.
- Install a hidden PowerShell tray shortcut.
- Expose the CDP endpoint to the local network.

Do not run untrusted local software while CDP is enabled.

## Repository layout

```text
windows/
├── app/CodexDreamSkin/    # Native WinUI 3 theme manager
├── assets/                # Default theme, CSS, injector payload, and sample artwork
├── presets/               # Bundled theme presets
├── scripts/               # Install, launch, apply, verify, and restore scripts
├── tests/                 # PowerShell, Node.js, and live UI regression tests
├── references/            # Windows runtime and QA notes
└── SKILL.md               # Codex automation workflow
```

## Development checks

```powershell
cd windows
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
dotnet build .\app\CodexDreamSkin\CodexDreamSkin.csproj -c Release -p:Platform=x64 -nologo
```

Every development change should also update [`DEVELOPMENT_PROGRESS.md`](./DEVELOPMENT_PROGRESS.md) and [`DEVELOPMENT_LOG.md`](./DEVELOPMENT_LOG.md).

## Known limitations

- The theme follows Codex's current Electron/Chromium DOM; a major Codex update may require selector updates.
- Normal-launch auto-apply is an optional user-level takeover feature. When disabled, a normal Codex launch does not automatically include the theme.
- Live UI tests require a locally running Codex instance with a managed CDP session.
- This is an unofficial project and is not affiliated with or endorsed by OpenAI.

## Acknowledgements

Thanks to [`Fei-Away/Codex-Dream-Skin`](https://github.com/Fei-Away/Codex-Dream-Skin) for the original idea and foundation for external Codex theming. This repository independently rebuilds and maintains that work for Windows.
