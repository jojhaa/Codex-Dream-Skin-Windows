import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const read = (...parts) => fs.readFileSync(path.join(root, ...parts), "utf8");
const service = read("app", "CodexDreamSkin", "Services", "ThemeCatalogService.cs");
const palette = read("app", "CodexDreamSkin", "Services", "ThemePaletteService.cs");
const model = read("app", "CodexDreamSkin", "Models", "ThemeDefinition.cs");
const page = read("app", "CodexDreamSkin", "Pages", "ThemesPage.xaml.cs");
const xaml = read("app", "CodexDreamSkin", "Pages", "ThemesPage.xaml");
const previewFixture = read("app", "CodexDreamSkin", "Controls", "CodexPreviewFixture.xaml");
const previewFixtureCode = read("app", "CodexDreamSkin", "Controls", "CodexPreviewFixture.xaml.cs");
const engine = read("app", "CodexDreamSkin", "Services", "CodexThemeEngine.cs");
const cdpClient = read("app", "CodexDreamSkin", "Services", "CdpClient.cs");
const locator = read("app", "CodexDreamSkin", "Services", "CodexPackageLocator.cs");
const processResolver = read("app", "CodexDreamSkin", "Services", "ProcessPathResolver.cs");
const listenerVerifier = read("app", "CodexDreamSkin", "Services", "TcpListenerVerifier.cs");
const mainWindow = read("app", "CodexDreamSkin", "MainWindow.xaml.cs");
const diagnosticsPage = read("app", "CodexDreamSkin", "Pages", "DiagnosticsPage.xaml.cs");
const diagnosticsXaml = read("app", "CodexDreamSkin", "Pages", "DiagnosticsPage.xaml");
const takeover = read("app", "CodexDreamSkin", "Services", "CodexTakeoverService.cs");
const processController = read("app", "CodexDreamSkin", "Services", "CodexProcessController.cs");
const managerSettings = read("app", "CodexDreamSkin", "Services", "ManagerSettingsService.cs");
const appStoragePaths = read("app", "CodexDreamSkin", "Services", "AppStoragePaths.cs");
const startupTasks = read("app", "CodexDreamSkin", "Services", "StartupTaskService.cs");
const releaseChecks = read("app", "CodexDreamSkin", "Services", "ReleaseCheckService.cs");
const dashboardPage = read("app", "CodexDreamSkin", "Pages", "DashboardPage.xaml.cs");
const dashboardXaml = read("app", "CodexDreamSkin", "Pages", "DashboardPage.xaml");
const freeSoftwareNotice = read("app", "CodexDreamSkin", "Models", "FreeSoftwareNotice.cs");
const zhResources = read("app", "CodexDreamSkin", "Strings", "zh-CN", "Resources.resw");
const enResources = read("app", "CodexDreamSkin", "Strings", "en-US", "Resources.resw");
const settingsPage = read("app", "CodexDreamSkin", "Pages", "SettingsPage.xaml.cs");
const settingsXaml = read("app", "CodexDreamSkin", "Pages", "SettingsPage.xaml");
const appLifecycle = read("app", "CodexDreamSkin", "App.xaml.cs");
const mainPage = read("app", "CodexDreamSkin", "MainPage.xaml.cs");
const mainXaml = read("app", "CodexDreamSkin", "MainPage.xaml");
const themeResources = read("app", "CodexDreamSkin", "Styles", "ThemeResources.xaml");
const trayIcon = read("app", "CodexDreamSkin", "Services", "TrayIconService.cs");
const program = read("app", "CodexDreamSkin", "Program.cs");
const manifest = read("app", "CodexDreamSkin", "Package.appxmanifest");
const project = read("app", "CodexDreamSkin", "CodexDreamSkin.csproj");
const iconBuilder = read("scripts", "build-app-icon.py");
const milkyWayTheme = JSON.parse(read("presets", "preset-milky-way-glass", "theme.json"));
const milkyWayImagePath = path.join(root, "presets", "preset-milky-way-glass", "background.png");

assert.equal(milkyWayTheme.id, "preset-milky-way-glass");
assert.equal(milkyWayTheme.name, "银河夜幕 · 星河玻璃");
assert.equal(milkyWayTheme.appearance, "dark");
assert.equal(milkyWayTheme.schemaVersion, 11);
assert.equal(milkyWayTheme.decorations.profile, "milky-way");
assert.equal(milkyWayTheme.surfaces.sidebarBackground, "continuous");
assert.equal(milkyWayTheme.surfaces.matchWorkspaceTransparency, true);
assert.equal(milkyWayTheme.image, "background.png");
assert.equal(milkyWayTheme.materials.dark.page, 0.30);
assert.equal(milkyWayTheme.materials.dark.sidebar, 0.44);
assert.equal(milkyWayTheme.materials.dark.composer, 0.36);
assert.equal(milkyWayTheme.materials.components.menus.dark.opacity, 0.58);
assert.equal(milkyWayTheme.materials.components.workspace.dark.opacity, 0.38);
assert.ok(fs.existsSync(milkyWayImagePath), "missing bundled Milky Way wallpaper");
const milkyWayImage = fs.readFileSync(milkyWayImagePath);
assert.equal(milkyWayImage.readUInt32BE(16), 1672, "Milky Way wallpaper width changed");
assert.equal(milkyWayImage.readUInt32BE(20), 941, "Milky Way wallpaper height changed");
for (const token of [
  "MilkyWayBundledThemeDirectory",
  "preset-(?:kanna-hashimoto|milky-way-glass)",
  "AllowedDecorationProfiles",
  "DecorationProfile",
  "decorations = new { profile = theme.DecorationProfile }",
  "AllowedSidebarBackgroundModes",
  "SidebarBackgroundMode",
  "MatchWorkspaceTransparency",
  "matchWorkspaceTransparency = theme.MatchWorkspaceTransparency",
])
  assert.ok(service.includes(token), `missing native Milky Way bundled-theme contract: ${token}`);
for (const token of [
  "preset-milky-way-glass\\background.png",
  "preset-milky-way-glass\\theme.json",
])
  assert.ok(project.includes(token), `missing published Milky Way asset: ${token}`);
for (const token of [
  "PreviewDecorationRibbon",
  "PreviewPolaroidImage",
])
  assert.ok(previewFixture.includes(token), `missing Kanna isolated decoration surface: ${token}`);
assert.ok(!previewFixture.includes("PreviewGalaxyConstellation"),
  "the Milky Way isolated preview must not retain a constellation overlay");
for (const token of [
  "ApplyDecorationProfile",
  "var isDecorated = !isMilkyWay && !isMinimal",
  "PreviewDecorationRibbon.Visibility = isDecorated",
  "PreviewPolaroidCard.Visibility = isDecorated",
])
  assert.ok(previewFixtureCode.includes(token), `missing decoration suppression behavior: ${token}`);
for (const token of ["Nocturne / 星河", "STELLAR HORIZON", "MILKY WAY · 02"])
  assert.ok(!previewFixtureCode.includes(token), `Milky Way decoration residue remains in the isolated preview: ${token}`);

for (const token of [
  "<Version>0.3.4</Version>",
  "<AssemblyVersion>0.3.4.0</AssemblyVersion>",
  "<FileVersion>0.3.4.0</FileVersion>",
])
  assert.ok(project.includes(token), `missing v0.3.4 assembly contract: ${token}`);
assert.ok(manifest.includes('Version="0.3.4.0"'),
  "the packaged identity must use v0.3.4");
assert.ok(releaseChecks.includes('UserAgent.ParseAdd("CodexDreamSkin/0.3.4")'),
  "release checks must identify the v0.3.4 client");

for (const token of [
  "Shell_NotifyIcon",
  "NimAdd",
  "NimDelete",
  'RegisterWindowMessage("TaskbarCreated")',
  "TrackPopupMenuEx",
  "WmLButtonDoubleClick",
  '"Assets", "AppIcon.ico"',
  "_dispatcherQueue.TryEnqueue",
  "CommandOpenThemes",
  "CommandOpenDiagnostics",
  "CommandOpenSettings",
  "CommandHide",
  "CommandExit",
])
  assert.ok(trayIcon.includes(token),
    `missing native notification-area contract: ${token}`);
for (const token of [
  "new TrayIconService(",
  "managerWindow.EnableCloseToTray()",
  "_trayIcon?.Dispose()",
  "managerWindow.ShowDestination",
])
  assert.ok(appLifecycle.includes(token),
    `missing notification-area lifecycle integration: ${token}`);
for (const token of [
  "public void NavigateTo(string tag)",
  '"themes" => typeof(ThemesPage)',
  '"diagnostics" => typeof(DiagnosticsPage)',
  '"settings" => typeof(SettingsPage)',
])
  assert.ok(mainPage.includes(token),
    `missing tray navigation contract: ${token}`);
for (const token of [
  'Background="{ThemeResource KannaShellOverlayBrush}"',
  'x:Name="GlobalHeader"',
  'x:Uid="StudioBrandEyebrow"',
  'x:Uid="StudioBrandTitle"',
  'x:Name="GlobalActions"',
  'x:Name="DashboardModeButton"',
  'x:Name="ThemesModeButton"',
  'x:Name="DiagnosticsModeButton"',
  'x:Name="SettingsModeButton"',
  'x:Name="WorkspaceModeContext"',
  'x:Name="ContentFrame"',
])
  assert.ok(mainXaml.includes(token),
    `missing flat application shell contract: ${token}`);
for (const token of ["<NavigationView", "ShellNavigationView", "PaneDisplayMode", "OpenPaneLength", "ShellRailColumn"])
  assert.ok(!mainXaml.includes(token),
    `legacy NavigationView shell must stay removed: ${token}`);
for (const token of [
  "private void ModeButton_Click",
  'NavigateTo("themes")',
  "WorkspaceModeContext.Text",
  "DashboardModeButton.Style",
  "ThemesModeButton.Style",
  "DiagnosticsModeButton.Style",
  "WorkspaceModeContext.Text",
])
  assert.ok(mainPage.includes(token),
    `missing flat shell behavior contract: ${token}`);
for (const token of [
  'x:Key="StudioPaneBrush"',
  'x:Key="StudioSurfaceBrush"',
  'x:Key="StudioGroupStyle"',
  'x:Key="WorkspaceTitleStyle"',
  'x:Key="WorkspaceSubtitleStyle"',
  'x:Key="SectionLabelStyle"',
  'x:Key="SettingsRowStyle"',
])
  assert.ok(themeResources.includes(token),
    `missing shared studio design resource: ${token}`);
for (const token of [
  'x:Name="DashboardPrimaryGrid"',
  'x:Name="DashboardArtwork"',
  'x:Name="DashboardFooterGrid"',
  'x:Name="RefreshStatusButton"',
  "DashboardPage_SizeChanged",
  "e.NewSize.Width >= 920",
])
  assert.ok(`${dashboardXaml}\n${dashboardPage}`.includes(token),
    `missing refactored overview workspace contract: ${token}`);
for (const token of [
  'x:Name="DiagnosticsWorkspace"',
  'x:Name="HealthPanel"',
  'x:Name="PortsPanel"',
  'Style="{StaticResource StudioGroupStyle}"',
  "DiagnosticsPage_SizeChanged",
  "e.NewSize.Width >= 980",
])
  assert.ok(`${diagnosticsXaml}\n${diagnosticsPage}`.includes(token),
    `missing refactored diagnostics workspace contract: ${token}`);
for (const token of [
  'x:Name="SettingsWorkspace"',
  'x:Name="SettingsRail"',
  'x:Name="SettingsScrollViewer"',
  'Style="{StaticResource SettingsRowStyle}"',
  "SettingsPage_SizeChanged",
  "e.NewSize.Width >= 920",
])
  assert.ok(`${settingsXaml}\n${settingsPage}`.includes(token),
    `missing refactored settings workspace contract: ${token}`);
for (const token of [
  "ShellBrandTitle",
  "ShellBrandSubtitle",
  "DiagnosticsHealthTitle",
  "SettingsSectionsTitle",
  "SettingsCategoryGeneral",
  "SettingsAutomationTitle",
  "SettingsUpdatesTitle",
  "StudioBrandEyebrow",
  "StudioBrandTitle",
  "WorkspaceModeDashboard",
  "WorkspaceModeThemes",
  "WorkspaceModeDiagnostics",
  "WorkspaceModeSettings",
])
  assert.ok(zhResources.includes(token) && enResources.includes(token),
    `missing localized whole-app studio resource: ${token}`);
for (const token of [
  "TrayTooltip",
  "TrayOpenManager",
  "TrayOpenThemes",
  "TrayOpenDiagnostics",
  "TrayOpenSettings",
  "TrayHide",
  "TrayExit",
])
  assert.ok(zhResources.includes(token) && enResources.includes(token),
    `missing localized notification-area resource: ${token}`);

assert.match(
  project,
  /<PublishTrimmed>False<\/PublishTrimmed>/,
  "Release builds must retain reflection metadata used by dynamic theme JSON payloads.",
);
assert.doesNotMatch(
  project,
  /<PublishTrimmed[^>]*>True<\/PublishTrimmed>/,
  "No configuration may re-enable trimming while reflection-based theme serialization remains in use.",
);
for (const token of [
  "<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>",
  'Include="Assets\\AppIcon.ico"',
  'CopyToOutputDirectory="PreserveNewest"',
  'CopyToPublishDirectory="PreserveNewest"',
  'AppWindow.SetIcon("Assets/AppIcon.ico")',
])
  assert.ok(`${project}\n${mainWindow}`.includes(token),
    `missing Windows application icon contract: ${token}`);
for (const token of [
  "ICON_SIZES = (16, 24, 32, 48, 64, 128, 256)",
  '"AppIconMaster.png"',
  '"AppIcon.ico"',
  '"Square150x150Logo.scale-200.png"',
  '"Square44x44Logo.scale-200.png"',
  '"StoreLogo.png"',
  '"Wide310x150Logo.scale-200.png"',
  '"SplashScreen.scale-200.png"',
  'alpha.getextrema()[0] < 255',
  'Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))',
])
  assert.ok(iconBuilder.includes(token),
    `missing reproducible Windows icon asset contract: ${token}`);
for (const token of [
  "<WindowsPackageType>None</WindowsPackageType>",
  "<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>",
  'Include="Assets\\Theme\\dream-reference.png"',
  'CopyToPublishDirectory="PreserveNewest"',
  "Environment.SpecialFolder.LocalApplicationData",
])
  assert.ok(`${project}\n${appStoragePaths}`.includes(token),
    `missing portable EXE runtime contract: ${token}`);
for (const source of [diagnosticsPage, settingsPage]) {
  assert.ok(source.includes("Microsoft.Windows.ApplicationModel.Resources"),
    "unpackaged pages must use the Windows App SDK MRT Core ResourceLoader");
  assert.ok(!source.includes("using Windows.ApplicationModel.Resources;"),
    "unpackaged pages must not use the package-identity-only legacy ResourceLoader");
}
for (const token of ["RunGuardedAsync", "DiagnosticsInfoBar.Severity = InfoBarSeverity.Error", "DiagnosticsRefreshFailed"])
  assert.ok(`${diagnosticsPage}\n${diagnosticsXaml}`.includes(token),
    `missing crash-safe diagnostics refresh contract: ${token}`);
assert.match(
  engine,
  /const content=!!document\.querySelector\('\.composer-surface-chrome, \[role=\\"main\\"\]'\)/,
  "The live probe must identify Codex content without requiring the optional sidebar.",
);
assert.match(
  engine,
  /location\.protocol === 'app:' && root && shell && content/,
  "A collapsed sidebar must not make the current Codex task ineligible for theme application.",
);
assert.doesNotMatch(
  engine,
  /location\.protocol === 'app:' && root && shell && sidebar/,
  "The live probe must not regress to requiring a visible sidebar.",
);

for (const token of [
  "MinimumWindowWidth = 770",
  "MinimumWindowHeight = 680",
  "AppWindow.Resize(new SizeInt32(1600, 900))",
  "WmGetMinMaxInfo = 0x0024",
  "SetWindowSubclass",
  "WindowNative.GetWindowHandle(this)",
  "minMaxInfo.MinimumTrackSize.X",
  "minMaxInfo.MinimumTrackSize.Y",
  "AppWindow.Changed += AppWindow_Changed",
])
  assert.ok(mainWindow.includes(token), `missing native minimum-window contract: ${token}`);

for (const token of [
  "FreeSoftwareNoticeCard", "FreeSoftwareNoticeBodyText", "OfficialProjectLink",
  "EnsureFreeSoftwareNotice", "FreeSoftwareNotice.IsCanonical",
  "本软件永久免费、开源", "若您通过付费渠道获得，请立即申请退款",
  "This software is permanently free and open source",
  "https://github.com/jojhaa/Codex-Dream-Skin-Windows",
])
  assert.ok(`${dashboardPage}\n${dashboardXaml}\n${freeSoftwareNotice}\n${zhResources}\n${enResources}`.includes(token),
    `missing protected free-software notice contract: ${token}`);
assert.ok(!dashboardXaml.includes('Text="70%"'), "obsolete migration percentage must not remain on the dashboard");
assert.ok(!dashboardXaml.includes("<ProgressBar"), "obsolete migration progress bar must not remain on the dashboard");

for (const token of [
  "MaximumPackageBytes = 32 * 1024 * 1024",
  "MaximumPackageExpandedBytes = 64L * 1024 * 1024",
  "MaximumMetadataBytes = 256 * 1024",
  "MaximumHistoryEntries = 20",
  "archive.Entries.Count is < 3 or > 8",
  "string.Equals(entry.Name, entry.FullName, StringComparison.Ordinal)",
  "主题包包含不允许的附加文件",
  "format = \"codex-dream-theme\"",
  "formatVersion = 1",
  "不支持的主题数据版本",
  "主题图片内容与文件扩展名不一致",
  "schemaVersion = 11",
  "compositions = new",
  "components = new",
]) assert.ok(service.includes(token), `missing package safety contract: ${token}`);

const saveStart = service.indexOf("public async Task SaveAsync");
const saveEnd = service.indexOf("public async Task SelectAsync", saveStart);
const saveBody = service.slice(saveStart, saveEnd);
assert.ok(saveBody.indexOf("CreateHistorySnapshotCoreAsync") < saveBody.indexOf("WriteThemeCoreAsync"),
  "save must snapshot the prior version before writing the new version");
assert.ok(service.includes("CleanupAbandonedImagesAsync"), "startup must clean abandoned staged images");
assert.ok(service.indexOf("File.OpenRead(currentThemePath)") < service.indexOf("Directory.EnumerateFiles(directory, \"*\""),
  "cleanup must parse the current metadata before deleting any image");

const restoreStart = service.indexOf("public async Task<ThemeDefinition> RestoreHistoryAsync");
const restoreEnd = service.indexOf("public async Task<ThemeDefinition> DuplicateAsync", restoreStart);
const restoreBody = service.slice(restoreStart, restoreEnd);
assert.ok(restoreBody.indexOf("ReadAllTextAsync") < restoreBody.indexOf("CreateHistorySnapshotCoreAsync"),
  "restore must read the selected snapshot before retention pruning");
assert.ok(restoreBody.indexOf("CreateHistorySnapshotCoreAsync") < restoreBody.indexOf("WriteThemeCoreAsync"),
  "restore must snapshot the current version before replacing it");

for (const token of ["ImportPackageButton_Click", "ExportButton_Click", "HistoryButton_Click", "ImageSlotButton_Click", "ResetImageSlot_Click", "SanitizeSuggestedFileName"])
  assert.ok(page.includes(token), `missing theme manager command: ${token}`);
for (const token of ["ImportThemePackageButton", "ExportThemeButton", "ThemeHistoryButton"])
  assert.ok(xaml.includes(token), `missing localized command button: ${token}`);
for (const token of ["BackgroundImageButton", "SidebarImageButton", "ComposerImageButton", "HomeImageButton", "HomeComposerImageButton", "PolaroidImageButton"])
  assert.ok(xaml.includes(token), `missing regional image slot: ${token}`);
for (const token of [
  "SidebarBackgroundModeComboBox",
  "ThemeSidebarBackgroundContinuous",
  "ThemeSidebarBackgroundIndependent",
  "ThemeSidebarBackgroundHint",
  "MatchWorkspaceTransparencyToggle",
  "ThemeMatchWorkspaceTransparency",
  "ThemeMatchWorkspaceTransparencyHint",
])
  assert.ok(xaml.includes(token), `missing sidebar background source control: ${token}`);
for (const token of ["SidebarResetButton", "ComposerResetButton", "HomeResetButton", "HomeComposerResetButton", "PolaroidResetButton"])
  assert.ok(xaml.includes(token), `missing regional image inheritance action: ${token}`);
for (const token of [
  "CompositionPreviewSurface", "CompositionFocusXSlider", "CompositionFocusYSlider",
  "CompositionZoomSlider", "CompositionFitComboBox", "CompositionOffsetXSlider",
  "CompositionOffsetYSlider", "ThemeCopyBackgroundCompositionButton",
  "ThemeResetCompositionButton", "CropToFrameButton", "ThemeCompositionCropSurface",
  "KeyDown=\"CompositionPreview_KeyDown\"", "ImageOpened=\"CompositionPreviewImage_ImageOpened\"",
  "ImageFailed=\"CompositionPreviewImage_ImageFailed\"", "PreviewDarkModeToggle", "PreviewContrastInfoBar",
  "FreshPaletteButton", "MidnightPaletteButton", "LowFogPaletteButton"
]) assert.ok(xaml.includes(token), `missing composition or palette manager control: ${token}`);
for (const token of ["ThemeComposition", "BackgroundComposition", "SidebarComposition", "ComposerComposition", "HomeComposition", "HomeComposerComposition", "PolaroidComposition", "SidebarBackgroundMode", "MatchWorkspaceTransparency"])
  assert.ok(model.includes(token), `missing regional composition model contract: ${token}`);
for (const token of [
  "public bool MatchWorkspaceTransparency",
  "PreviewHomeWorkspaceMaterial",
  "PreviewHomeHeaderMaterial",
])
  assert.ok(previewFixtureCode.includes(token), `missing isolated transparency matching behavior: ${token}`);
for (const token of [
  "CodexPageFixture.MatchWorkspaceTransparency",
  "MatchWorkspaceTransparencyToggle.IsEnabled",
])
  assert.ok(page.includes(token), `missing transparency matching editor behavior: ${token}`);
for (const token of ["ThemeComponentMaterial", "ThemeComponentMaterials", "ThemeComponentSlot", "Messages", "Summaries", "Previews", "Menus", "Workspace", "Code", "Suggestions"])
  assert.ok(model.includes(token), `missing component material model contract: ${token}`);
for (const token of [
  "ComponentMaterialSlotComboBox", "LightComponentColorTextBox", "LightComponentOpacitySlider",
  "DarkComponentColorTextBox", "DarkComponentOpacitySlider", "ResetComponentMaterialButton",
  "ThemeComponentPreviews"
]) assert.ok(xaml.includes(token), `missing component material editor control: ${token}`);
for (const token of ["ReadComponentMaterialControls", "UpdateComponentMaterialPreview", "ResetComponentMaterialButton_Click"])
  assert.ok(page.includes(token), `missing component material editor behavior: ${token}`);
for (const token of [
  "Runtime.addBinding", "__dreamSkinCommand", "Runtime.bindingCalled", "Input.dispatchKeyEvent",
  "DreamShortcuts", "NativeMenuCommands", "showApplicationMenu", "AutomationElement.RootElement",
  "InvokePattern.Pattern", "\"file-menu\"", "\"help-menu\""
])
  assert.ok(engine.includes(token), `missing trusted translated application-menu bridge: ${token}`);
for (const token of [
  "TryGetIdentity", "GetPackageFullName", "GetPackageFamilyName", "PackageFamilyName",
  "PackageSignatureKind.Store", "当前动态 Store 包身份", "installation.PackageFullName",
  "LastManagedPort = 9345", "FindTrustedListener", "FindAvailablePort", "ListenerPort"
])
  assert.ok(`${engine}\n${locator}\n${processResolver}\n${listenerVerifier}`.includes(token),
    `missing update-safe dynamic package identity contract: ${token}`);
for (const token of [
  "InspectManagedPorts", "TerminateManagedListenerAsync", "TerminateWithElevationAsync",
  "\"taskkill.exe\"", "Verb = \"runas\"", "ClosePortProcessButton_Click",
  "PortRowsControl", "RefreshPortsButton", "ContentDialog"
])
  assert.ok(`${listenerVerifier}\n${diagnosticsPage}\n${diagnosticsXaml}`.includes(token),
    `missing managed port inspection and close-process contract: ${token}`);
for (const token of [
  "AutoTakeoverStockCodex", "ApplicationData.Current.LocalSettings",
  "CodexDreamSkinMonitor", "StartupTask.GetAsync", "RequestEnableAsync", "task.Disable()",
  "Microsoft.Win32", "CurrentVersion\\Run", "CodexDreamSkin", "--startup",
  "Registry.CurrentUser", "Environment.ProcessPath", "RegistryValueKind.String"
])
  assert.ok(`${managerSettings}\n${startupTasks}`.includes(token),
    `missing persisted native startup contract: ${token}`);
for (const token of [
  "FindOrRegisterForKey", "RedirectActivationToAsync", "DISABLE_XAML_GENERATED_MAIN",
  "ExtendedActivationKind.StartupTask", "PortableStartupArgument", "HideToBackground", "ShowAndActivate",
  "TakeoverService.Start", "TakeoverService.DisposeAsync"
])
  assert.ok(`${program}\n${project}\n${appLifecycle}\n${mainWindow}`.includes(token),
    `missing single-instance background lifecycle contract: ${token}`);
for (const token of [
  "FindTrustedListener", "candidateObservations < 2", "CloseCurrentPackageAsync",
  "PackageFullName", "CloseMainWindow", "Kill(entireProcessTree: true)",
  "StartOrApplyAsync", "_engine.Snapshot.ListenerPort != trustedListener.Value.Port",
  "DateTimeOffset.UtcNow.AddSeconds(15)"
])
  assert.ok(`${takeover}\n${processController}`.includes(token),
    `missing safe ordinary-launch takeover contract: ${token}`);
for (const token of [
  "AutoTakeoverToggle", "StartupTaskToggle", "ExitManagerButton",
  "ContentDialog", "AutoTakeoverDialogBody", "SetEnabledAsync(true)",
  "ManagerSettings.AutoTakeoverEnabled = true", "ManagerSettings.AutoTakeoverEnabled = false"
])
  assert.ok(`${settingsXaml}\n${settingsPage}`.includes(token),
    `missing takeover settings UX contract: ${token}`);
for (const token of [
  "VersionAndAuthenticityCard", "CurrentVersionText", "CheckUpdatesButton",
  "LatestReleaseLink", "VersionCheckProgressRing", "CheckUpdatesButton_Click",
  "ReleaseChecks.CheckLatestAsync", "VersionAlreadyCurrent", "VersionUpdateAvailable",
  "api.github.com/repos/jojhaa/Codex-Dream-Skin-Windows/releases/latest",
  "application/vnd.github+json", "X-GitHub-Api-Version", "2022-11-28",
  "UserAgent.ParseAdd", "TryGetTrustedReleaseUri", "CompareTo",
])
  assert.ok(`${settingsXaml}\n${settingsPage}\n${releaseChecks}`.includes(token),
    `missing safe release-check contract: ${token}`);
for (const token of [
  "FreeSoftwareNoticeTitle", "FreeSoftwareNoticeBodyText", "OfficialProjectLink",
  "FreeSoftwareNotice.IsCanonical", "FreeSoftwareNotice.ProjectUrl",
])
  assert.ok(`${settingsXaml}\n${settingsPage}`.includes(token),
    `missing Settings authenticity notice contract: ${token}`);
for (const token of [
  'xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"',
  'Category="windows.startupTask"', 'TaskId="CodexDreamSkinMonitor"',
  'EntryPoint="Windows.FullTrustApplication"', 'Enabled="false"'
])
  assert.ok(manifest.includes(token), `missing packaged StartupTask manifest contract: ${token}`);
for (const token of ["AnalyzeAsync", "LooksLikeSkin", "\"midnight\"", "\"lowfog\"", "EnsureCoolAccent"])
  assert.ok(palette.includes(token), `missing automatic palette contract: ${token}`);
for (const token of [
  "ApplyCompositionToImage", "CopyBackgroundComposition_Click", "ResetComposition_Click", "CropToFrame_Click", "CompositionSelectionFrame", "UpdateSelectionFrame",
  "CompositionPreview_PointerMoved", "CompositionPreview_PointerWheelChanged", "CompositionPreview_KeyDown",
  "CompleteCropGesture", "CompositionPreviewImage_ImageOpened", "CompositionSelectionCanvas.Visibility = Visibility.Collapsed",
  "BitmapDecoder.CreateAsync", "_sourceImageSizes", "ReferenceEqualityComparer.Instance",
  "取景 {_selectionWidth:F0}×{_selectionHeight:F0}",
  "Math.Min(displayWidth / sourceWidth, displayHeight / sourceHeight)", "UIElement.PointerPressedEvent", "UIElement.PointerWheelChangedEvent",
  "UpdateContrastWarning", "ApplyPalette_Click"
])
  assert.ok(page.includes(token), `missing composition or preview behavior: ${token}`);
const wheelStart = page.indexOf("private void CompositionPreview_PointerWheelChanged");
const wheelEnd = page.indexOf("private void CompositionPreview_KeyDown", wheelStart);
const wheelBody = page.slice(wheelStart, wheelEnd);
for (const token of ["InputKeyboardSource.GetKeyStateForCurrentThread", "VirtualKey.Control", "CoreVirtualKeyStates.Down", "if ((controlState &", "e.Handled = true"])
  assert.ok(wheelBody.includes(token), `missing modifier-safe crop wheel behavior: ${token}`);
assert.ok(wheelBody.indexOf("if ((controlState &") < wheelBody.indexOf("ChangeCropZoom"),
  "plain wheel input must return before crop zoom consumes the event");
for (const token of [
  "SidebarModePreviewSurface", "MessageModePreviewSurface", "ComposerModePreviewSurface",
  "HomeModePreviewSurface", "HomeComposerModePreviewSurface", "PolaroidModePreviewSurface"
]) assert.ok(xaml.includes(token), `missing true-ratio appearance preview surface: ${token}`);
for (const token of [
  "SizeAppearancePreview", "_regionMetrics?.Get(slot)",
  "new ThemeRegionSize(736, 98)", "new ThemeRegionSize(1603, 215)", "new ThemeRegionSize(920, 98)",
  "_sourceImageSizes.TryGetValue(image.Source, out var decodedSize)", "CreatePreviewGradient",
  "opacities[2] * composerMultipliers[3]", "PolaroidModePreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))",
  "Canvas.SetLeft(image, (width - renderedWidth) * positionX)", "Canvas.SetTop(image, (height - renderedHeight) * positionY)"
]) assert.ok(page.includes(token), `missing live-ratio appearance preview behavior: ${token}`);
assert.equal((xaml.match(/<Canvas><Image x:Name="(?:Sidebar|Message|Composer|Home|HomeComposer|Polaroid)ModePreviewImage/g) || []).length, 6,
  "all six appearance images must use post-position Canvas clipping rather than pre-clipped render transforms");
for (const token of ["LivePreviewToggle", "LivePreviewProgressRing", "LivePreviewStatusText"])
  assert.ok(xaml.includes(token), `missing live-preview control: ${token}`);
assert.ok(xaml.indexOf('x:Name="LivePreviewStatusText"') > xaml.indexOf('x:Name="CenterPreviewHost"'),
  "live-preview status must remain visible in the center preview header");
assert.ok(page.includes("LivePreviewToggle.IsEnabled = !_isBusy && _selectedTheme is not null"),
  "real Codex capture must be available for the selected bundled theme");
assert.ok(!page.includes("!LivePreviewToggle.IsOn || _selectedTheme?.IsBundled != false"),
  "bundled themes must not be rejected by the real-capture scheduler");
for (const token of [
  "x:Name=\"ThemesHeader\"",
  "BorderThickness=\"0,0,0,1\"",
  "Grid.Column=\"1\" Background=\"Transparent\" DefaultLabelPosition=\"Right\" IsDynamicOverflowEnabled=\"True\""
]) assert.ok(xaml.includes(token), `missing flat theme command strip: ${token}`);
for (const token of [
  "x:Name=\"ThemesWorkspace\" Grid.Row=\"2\" ColumnSpacing=\"0\"",
  "x:Name=\"LibraryColumn\" Width=\"260\"",
  "x:Name=\"LibraryPanel\"",
  "Background=\"{ThemeResource StudioPaneBrush}\"",
  "x:Name=\"ThemeLibraryRailButton\"",
  "x:Name=\"ThemeToolsRailButton\"",
  "x:Name=\"ThemeLibraryRail\"",
  "x:Name=\"ThemeToolsRail\"",
  "Grid.Column=\"0\"",
  "x:Name=\"ThemesList\"",
  "<ItemsStackPanel Orientation=\"Vertical\" />",
  "x:Uid=\"ThemeStudioToolsTitle\"",
  "x:Uid=\"ThemeToolOverview\"",
  "x:Uid=\"ThemeToolLocalization\"",
  "x:Uid=\"ThemeToolFloatingWindows\"",
  "x:Name=\"EditorPanel\" Grid.Column=\"1\" Grid.ColumnSpan=\"2\" ColumnSpacing=\"0\"",
  "x:Name=\"InspectorColumn\" Width=\"340\"",
  "x:Name=\"CenterPreviewHost\"",
  "x:Uid=\"ThemeStudioPreviewTitle\"",
  "x:Name=\"PreviewSurface\"",
  "MinHeight=\"420\"",
  "x:Name=\"InspectorScrollViewer\"",
  "x:Name=\"EditorCanvasHost\"",
  "x:Name=\"EditorCanvasPanel\"",
  "x:Name=\"ThemeImagesEditorExpander\"",
  "x:Name=\"CompositionEditorExpander\"",
  "HorizontalContentAlignment=\"Stretch\"",
  "x:Name=\"CompositionPreviewFrame\" Height=\"180\" HorizontalAlignment=\"Stretch\"",
  "x:Name=\"InspectorChrome\"",
  "x:Name=\"InspectorHost\"",
  "x:Name=\"InspectorPanel\"",
  "x:Name=\"PlannedToolPanel\"",
  "x:Name=\"HistoryInspectorPanel\""
]) assert.ok(xaml.includes(token), `missing three-pane theme-studio contract: ${token}`);
for (const token of [
  "private void ThemeTool_Checked",
  "private void ThemeRailButton_Click",
  "ThemeLibraryRail.Visibility = showLibrary",
  "ThemeToolsRail.Visibility = showLibrary",
  "private void SelectThemeTool",
  "EditorCanvasHost.Visibility = isBackground || isAssets",
  "PlannedToolPanel.Visibility = isPlanned",
  "HistoryInspectorPanel.Visibility = isHistory",
  "var pageWidth = e.NewSize.Width",
  "LibraryColumn.Width = new GridLength(pageWidth >= 1440 ? 270 : pageWidth >= 1180 ? 240 : 220)",
  "InspectorColumn.Width = new GridLength(pageWidth >= 1440 ? 340 : pageWidth >= 1180 ? 320 : 300)",
  "var compactMaterials = InspectorColumn.Width.Value < 620",
  "MaterialsGrid.ColumnDefinitions[1].Width = compactMaterials",
  "ComponentMaterialsGrid.ColumnDefinitions[1].Width = compactMaterials",
  "Grid.SetColumn(previewCards[index], index % 2)",
  "Grid.SetRow(previewCards[index], index / 2)",
  "EditorCanvasHost.ActualWidth - 4",
  "var frameWidth = available",
  "Math.Min(frameWidth / sourceRatio, maximumHeight)",
  "InspectorHost.ActualWidth - 28"
]) assert.ok(page.includes(token), `missing responsive theme-studio behavior: ${token}`);
for (const token of [
  "AppearancePreviewGrid.ColumnDefinitions[1].Width = compactMaterials",
  "var singleColumn = AppearancePreviewGrid.ColumnDefinitions[1].Width.IsAbsolute",
  "const double minimumHeight = 240"
]) assert.ok(!page.includes(token), `preview/composition layout must not switch at narrow width: ${token}`);
for (const token of [
  "x:Name=\"AppearancePreviewExpander\"",
  "x:Name=\"AutoPaletteExpander\"",
  "x:Name=\"PaletteActionsGrid\"",
  "x:Name=\"AccentPickerExpander\"",
  "x:Name=\"MaterialsEditorExpander\"",
  "x:Name=\"ComponentMaterialsEditorExpander\""
]) assert.ok(xaml.includes(token), `missing full-width inspector section: ${token}`);
assert.ok((xaml.match(/HorizontalContentAlignment=\"Stretch\"/g) || []).length >= 6,
  "composition and inspector expanders must stretch their content presenters");
assert.equal((xaml.match(/<ScrollViewer\b/g) || []).length, 3,
  "the studio must use dedicated tool, preview-task, and inspector scroll owners");
for (const token of [
  'x:Name="PreviewHomeScene"',
  'x:Name="PreviewTaskScene"',
  'x:Name="PreviewSettingsScene"',
  'x:Name="PreviewComposerTextBox"',
  'x:Name="PreviewConversationResult"',
  'Click="SceneButton_Click"',
  'Click="SendButton_Click"',
  'x:Uid="PreviewChromeMenus"',
  'x:Uid="PreviewHomePromptPrefix"',
  'x:Uid="PreviewSettingsIsolationInfo"',
  'x:Uid="PreviewFooterIsolation"',
]) assert.ok(previewFixture.includes(token), `missing complete isolated Codex page fixture: ${token}`);
for (const token of [
  "public void ResetInteraction",
  "public void ShowScene",
  "public ImageSource? BackgroundSource",
  "public ImageSource? SidebarSource",
  "public bool UseContinuousSidebarBackground",
  "PreviewSidebarMaterial.Background",
  "PreviewHomeHeaderMaterial.Background",
  "PreviewHomeWorkspaceMaterial.Background",
  "PreviewTaskSidebarMaterial.Background",
  "PreviewTaskHeaderMaterial.Background",
  "PreviewSettingsSidebarMaterial.Background",
  "PreviewMaterial(value",
  "Color.FromArgb(0x00, 0x00, 0x00, 0x00)",
  "public void SetFocus",
  "public bool TryNormalizePoint",
  "private void SceneButton_Click",
  "private void SendMessage",
  "PreviewConversationResult.Visibility = Visibility.Visible",
]) assert.ok(previewFixtureCode.includes(token), `missing isolated Codex page interaction: ${token}`);
for (const token of [
  "<Viewbox",
  'Width="1922" Height="1034"',
  'x:Name="PreviewBackgroundImage"',
  'x:Name="PreviewSidebarImage"',
  'x:Name="PreviewSidebarMaterial"',
  'x:Name="PreviewHomeHeaderMaterial"',
  'x:Name="PreviewHomeWorkspaceMaterial"',
  'x:Name="PreviewTaskSidebarMaterial"',
  'x:Name="PreviewTaskHeaderMaterial"',
  'x:Name="PreviewSettingsSidebarMaterial"',
  'Grid.RowSpan="2"',
  'Stretch="UniformToFill"',
  '<ColumnDefinition Width="275" />',
  'Canvas.Left="378" Canvas.Top="771" Width="906" Height="164"',
  'Canvas.Left="1510" Canvas.Top="716" Width="110" Height="156"',
  'Width="906" MinHeight="100"',
  'x:Uid="PreviewEnvironmentTitle"',
  'x:Uid="PreviewSettingsAppearanceTitle"',
  'x:Uid="PreviewSettingsSearchBox"',
])
  assert.ok(previewFixture.includes(token), `missing real-Codex geometry contract: ${token}`);
assert.ok(xaml.includes("<controls:CodexPreviewFixture"),
  "the theme page must host the scaled complete Codex fixture");
assert.ok(xaml.includes('x:Name="PreviewImage" Visibility="Collapsed"'),
  "the background must not use a second outer scaling coordinate system");
for (const token of [
  "CodexPageFixture.BackgroundSource = source",
  "CodexPageFixture.SidebarSource = sidebar",
  "CodexPageFixture.UseContinuousSidebarBackground",
  "CodexPageFixture.TryNormalizePoint",
  "CodexPageFixture.SetFocus",
])
  assert.ok(page.includes(token), `preview background and focus must share the fixture scale: ${token}`);
for (const token of [
  'x:Uid="PreviewSceneHomeButton"',
  'x:Uid="PreviewSceneTaskButton"',
  'x:Uid="PreviewSceneSettingsButton"',
])
  assert.ok(xaml.includes(token), `scene switching must stay outside the Codex chrome: ${token}`);
for (const token of [
  'x:Uid="PreviewSuggestionExplore"',
  'x:Uid="PreviewSuggestionBuild"',
  'x:Uid="PreviewSuggestionReview"',
  'x:Uid="PreviewSuggestionFix"',
])
  assert.ok(!previewFixture.includes(token), `the supplied Codex screenshot has no suggestion-card row: ${token}`);
for (const token of [
  "PreviewChromeMenus.Text", "PreviewSceneHomeButton.Content", "PreviewSceneTaskButton.Content",
  "PreviewSceneSettingsButton.Content", "PreviewHomePromptPrefix.Text", "PreviewSelectedProjectPill.Text",
  "PreviewHomePromptSuffix.Text", "PreviewProjectSelectorLabel.Text", "PreviewComposerTextBox.PlaceholderText",
  "PreviewTaskTitle.Text", "PreviewSettingsTitle.Text", "PreviewSettingsGeneralHeading.Text",
  "PreviewSettingsNotificationsLabel.Text", "PreviewSettingsIsolationInfo.Message",
  "PreviewFooterIsolation.Text"
]) assert.ok(zhResources.includes(token) && enResources.includes(token),
  `missing localized complete Codex preview resource: ${token}`);
for (const token of [
  'x:Uid="PreviewSettingsAppearanceTitle"',
  'x:Uid="PreviewSettingsNotificationsLabel"',
])
  assert.ok(previewFixture.includes(token), `missing property-safe Settings preview Uid: ${token}`);
for (const token of [
  "PreviewSettingsGeneral.Text",
  "PreviewSettingsNotifications.Text",
])
  assert.ok(!zhResources.includes(token) && !enResources.includes(token),
    `a Button/TextBlock shared Uid would crash MRT localization at startup: ${token}`);
for (const token of ["PageScrollViewer", "EditorCanvasScrollViewer", "MaxHeight=\"620\""])
  assert.ok(!xaml.includes(token), `legacy document scrolling must stay removed: ${token}`);
for (const token of ["Grid.SetColumn(EditorPanel", "Grid.SetRow(EditorPanel", "Grid.SetColumn(InspectorChrome", "Grid.SetRow(InspectorChrome"])
  assert.ok(!page.includes(token), `theme studio must not dynamically reparent layout surfaces: ${token}`);
assert.ok(!xaml.includes('x:Name="EditorPanel" Grid.Column="1" Style="{StaticResource StatusCardStyle}"'),
  "the editor workspace must not restore the former nested card shell");
for (const token of [
  "ThemeStudioToolsTitle.Text", "ThemeToolOverview.Content", "ThemeToolColorMaterials.Content",
  "ThemeRailLibraryButton.Content", "ThemeRailToolsButton.Content", "ThemeStudioDraftLabel.Text",
  "ThemeToolTypography.Content", "ThemeToolBackgroundComposition.Content", "ThemeToolInterfaceAssets.Content",
  "ThemeToolDecoration.Content", "ThemeToolComponentStyles.Content", "ThemeToolLayoutSpacing.Content",
  "ThemeToolLocalization.Content", "ThemeToolFloatingWindows.Content", "ThemeToolThemeInfo.Content",
  "ThemeToolHistory.Content", "ThemeStudioPreviewTitle.Text", "ThemeStudioPlannedToolInfo.Message"
]) assert.ok(zhResources.includes(token) && enResources.includes(token),
  `missing localized three-pane theme-studio resource: ${token}`);
for (const token of ["ScheduleLivePreview", "Task.Delay(240", "CancelLivePreviewWork", "RefreshPreviewAsync"])
  assert.ok(page.includes(token), `missing debounced live-preview behavior: ${token}`);
for (const token of ["RefreshPreviewAsync", "Page.removeScriptToEvaluateOnNewDocument", "_earlyScriptIdentifiers"])
  assert.ok(engine.includes(token), `missing incremental CDP refresh behavior: ${token}`);
for (const token of [
  "public async Task<CodexPreviewFrame?> CapturePreviewFrameAsync",
  "requestAnimationFrame(() => requestAnimationFrame",
  "document.getElementById('codex-dream-skin-style')",
  "session.CaptureScreenshotAsync",
])
  assert.ok(engine.includes(token), `missing real Codex frame capture behavior: ${token}`);
for (const token of [
  "public async Task<byte[]> CaptureScreenshotAsync",
  '"Page.captureScreenshot"',
  "maximumScreenshotBytes",
  "bytes[0] != 0x89",
])
  assert.ok(cdpClient.includes(token), `missing bounded PNG screenshot decoding: ${token}`);
for (const token of [
  'x:Name="LivePreviewFrameHost"',
  'x:Name="LivePreviewFrameViewbox"',
  'x:Name="LivePreviewFrameCanvas"',
  'x:Name="LivePreviewFrameImage"',
  'x:Name="LivePreviewFrameBadge"',
  'SizeChanged="LivePreviewFrameHost_SizeChanged"',
  'Stretch="Uniform"',
  'Stretch="Fill"',
])
  assert.ok(xaml.includes(token), `missing real Codex preview frame surface: ${token}`);
for (const token of [
  "RefreshLivePreviewFrameAsync",
  "_engine.CapturePreviewFrameAsync",
  "InMemoryRandomAccessStream",
  "LivePreviewFrameCanvas.Width = frame.Width",
  "LivePreviewFrameCanvas.Height = frame.Height",
  "UpdateLivePreviewFrameStatus",
  "PreviewSurface.ActualWidth",
  "availableWidth / _livePreviewFrameWidth",
  "ShowOfflinePreviewFrame",
  "PreviewInteractionToolbar.IsHitTestVisible = false",
])
  assert.ok(page.includes(token), `missing live/offline preview frame switching: ${token}`);
for (const token of ["_previewSourceSignature", "canReuseImages", "MapThemeImages"])
  assert.ok(service.includes(token), `missing preview image reuse behavior: ${token}`);
assert.ok(page.includes("SidebarImageFileName == previousBackground ? stagedName"),
  "changing the main background must carry every inherited regional slot forward");

console.log("PASS: theme package trust boundary, history ordering, and manager commands.");
