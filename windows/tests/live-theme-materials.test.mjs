const port = Number(process.env.CODEX_CDP_PORT || 9335);
const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => response.json());
const target = targets.find((entry) => entry.type === "page" && entry.url === "app://-/index.html");
if (!target?.webSocketDebuggerUrl) throw new Error("Codex page target was not found.");

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});

const result = await new Promise((resolve, reject) => {
  const requestId = 1;
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (message.id !== requestId) return;
    if (message.error) reject(new Error(message.error.message));
    else if (message.result.exceptionDetails) reject(new Error(message.result.exceptionDetails.text));
    else resolve(message.result.result.value);
  });
  socket.send(JSON.stringify({
    id: requestId,
    method: "Runtime.evaluate",
    params: {
      returnByValue: true,
      expression: `(() => {
        const style = document.documentElement.style;
        const names = [
          '--dream-custom-light-page', '--dream-custom-light-sidebar', '--dream-custom-light-composer', '--dream-custom-light-card',
          '--dream-custom-dark-page', '--dream-custom-dark-sidebar', '--dream-custom-dark-composer', '--dream-custom-dark-card'
        ];
        const compositionNames = ['background', 'sidebar', 'composer', 'home', 'home-composer', 'polaroid']
          .flatMap((slot) => ['position', 'size', 'zoom'].map((property) => '--dream-' + slot + '-' + property));
        const componentNames = ['messages', 'summaries', 'previews', 'menus', 'workspace', 'code', 'suggestions']
          .flatMap((slot) => ['light', 'dark'].flatMap((mode) => ['rgb', 'opacity']
            .map((property) => '--dream-component-' + slot + '-' + mode + '-' + property)));
        const sidebar = document.querySelector('aside.app-shell-left-panel');
        const main = document.querySelector('main.main-surface, main[data-app-shell-main-surface]');
        const header = main?.querySelector(':scope > header.app-header-tint, :scope > header[data-app-shell-application-menu-bar]');
        return {
          version: window.__CODEX_DREAM_SKIN_STATE__?.version || null,
          decorationProfile: window.__CODEX_DREAM_SKIN_STATE__?.decorationProfile || null,
          sidebarBackgroundMode: window.__CODEX_DREAM_SKIN_STATE__?.sidebarBackgroundMode || null,
          matchWorkspaceTransparency: window.__CODEX_DREAM_SKIN_STATE__?.matchWorkspaceTransparency === true,
          decorationDataset: document.documentElement.dataset.dreamDecoration || null,
          sidebarBackgroundDataset: document.documentElement.dataset.dreamSidebarBackground || null,
          transparencyMatchDataset: document.documentElement.dataset.dreamTransparencyMatch || null,
          decorationText: document.getElementById('codex-dream-skin-chrome')?.textContent || '',
          decorationElementCount: document.getElementById('codex-dream-skin-chrome')?.childElementCount ?? -1,
          inheritedTaskEditionPresent: Boolean(document.querySelector('#codex-dream-skin-chrome .dream-task-edition')),
          inheritedRibbonPresent: Boolean(document.querySelector('#codex-dream-skin-chrome .dream-ribbon')),
          viewportArtwork: getComputedStyle(document.body, '::before').backgroundImage,
          sidebarArtwork: sidebar ? getComputedStyle(sidebar).backgroundImage : '',
          sidebarBackgroundColor: sidebar ? getComputedStyle(sidebar).backgroundColor : '',
          sidebarBackdropFilter: sidebar ? getComputedStyle(sidebar).backdropFilter : '',
          sidebarBeforeDisplay: sidebar ? getComputedStyle(sidebar, '::before').display : '',
          workspaceArtwork: main ? getComputedStyle(main).backgroundImage : '',
          workspaceBackgroundColor: main ? getComputedStyle(main).backgroundColor : '',
          workspaceBackdropFilter: main ? getComputedStyle(main).backdropFilter : '',
          headerArtwork: header ? getComputedStyle(header).backgroundImage : '',
          headerBackgroundColor: header ? getComputedStyle(header).backgroundColor : '',
          headerBackdropFilter: header ? getComputedStyle(header).backdropFilter : '',
          tokens: Object.fromEntries(names.map((name) => [name, style.getPropertyValue(name).trim()])),
          compositions: Object.fromEntries(compositionNames.map((name) => [name, style.getPropertyValue(name).trim()])),
          components: Object.fromEntries(componentNames.map((name) => [name, style.getPropertyValue(name).trim()]))
        };
      })()`
    }
  }));
});
socket.close();

const expected = {
  "--dream-custom-light-page": process.env.EXPECT_LIGHT_PAGE,
  "--dream-custom-light-sidebar": process.env.EXPECT_LIGHT_SIDEBAR,
  "--dream-custom-light-composer": process.env.EXPECT_LIGHT_COMPOSER,
  "--dream-custom-light-card": process.env.EXPECT_LIGHT_CARD,
  "--dream-custom-dark-page": process.env.EXPECT_DARK_PAGE,
  "--dream-custom-dark-sidebar": process.env.EXPECT_DARK_SIDEBAR,
  "--dream-custom-dark-composer": process.env.EXPECT_DARK_COMPOSER,
  "--dream-custom-dark-card": process.env.EXPECT_DARK_CARD,
};
const checks = Object.entries(expected)
  .filter(([, value]) => value !== undefined)
  .map(([name, value]) => ({ name, expected: Number(value).toFixed(3), actual: result.tokens[name] }));
const expectedPositions = Object.fromEntries(
  ['background', 'sidebar', 'composer', 'home', 'home-composer', 'polaroid'].map((slot) => {
    const environmentName = `EXPECT_${slot.replace('-', '_').toUpperCase()}_POSITION`;
    return [slot, process.env[environmentName] || '64.00% 44.00%'];
  }));
const expectedComponents = {
  "--dream-component-messages-dark-opacity": process.env.EXPECT_MESSAGES_DARK_OPACITY,
  "--dream-component-menus-dark-opacity": process.env.EXPECT_MENUS_DARK_OPACITY,
  "--dream-component-workspace-dark-opacity": process.env.EXPECT_WORKSPACE_DARK_OPACITY,
  "--dream-component-code-dark-opacity": process.env.EXPECT_CODE_DARK_OPACITY,
};
const componentChecks = Object.entries(expectedComponents)
  .filter(([, value]) => value !== undefined)
  .map(([name, value]) => ({ name, expected: Number(value).toFixed(3), actual: result.components[name] }));
const expectedDecorationProfile = process.env.EXPECT_DECORATION_PROFILE;
const expectedSidebarBackgroundMode = process.env.EXPECT_SIDEBAR_BACKGROUND_MODE;
const expectedMatchWorkspaceTransparency = process.env.EXPECT_MATCH_WORKSPACE_TRANSPARENCY;
const requiredDecorationText = (process.env.EXPECT_DECORATION_TEXT || "")
  .split("|")
  .map((value) => value.trim())
  .filter(Boolean);
const forbiddenDecorationText = (process.env.FORBID_DECORATION_TEXT || "")
  .split("|")
  .map((value) => value.trim())
  .filter(Boolean);
const expectedDecorationElementCount = process.env.EXPECT_DECORATION_ELEMENT_COUNT;
const pass = result.version === "3.10.0"
  && (!expectedDecorationProfile || (
    result.decorationProfile === expectedDecorationProfile
    && result.decorationDataset === expectedDecorationProfile))
  && (!expectedSidebarBackgroundMode || (
    result.sidebarBackgroundMode === expectedSidebarBackgroundMode
    && result.sidebarBackgroundDataset === expectedSidebarBackgroundMode))
  && (expectedMatchWorkspaceTransparency === undefined || (
    result.matchWorkspaceTransparency === (expectedMatchWorkspaceTransparency === "true")
    && result.transparencyMatchDataset === (expectedMatchWorkspaceTransparency === "true" ? "on" : "off")))
  && requiredDecorationText.every((value) => result.decorationText.includes(value))
  && forbiddenDecorationText.every((value) => !result.decorationText.includes(value))
  && (expectedDecorationElementCount === undefined
    || result.decorationElementCount === Number(expectedDecorationElementCount))
  && (expectedDecorationProfile !== "milky-way"
    || (!result.inheritedTaskEditionPresent && !result.inheritedRibbonPresent))
  && (expectedSidebarBackgroundMode !== "continuous" || (
    /url\(["']?blob:/.test(result.viewportArtwork)
    && result.sidebarArtwork === "none"
    && result.sidebarBackgroundColor === "rgba(0, 0, 0, 0)"
    && result.sidebarBackdropFilter === "none"
    && result.sidebarBeforeDisplay === "none"))
  && (expectedMatchWorkspaceTransparency !== "true" || (
    result.workspaceArtwork === "none"
    && result.workspaceBackgroundColor === "rgba(0, 0, 0, 0)"
    && result.workspaceBackdropFilter === "none"
    && (!result.headerArtwork || (
      result.headerArtwork === "none"
      && result.headerBackgroundColor === "rgba(0, 0, 0, 0)"
      && result.headerBackdropFilter === "none"))))
  && (expectedMatchWorkspaceTransparency !== "false" || result.workspaceArtwork !== "none")
  && Object.values(result.tokens).every((value) => /^(?:0|1|0?\.\d{3})$/.test(value))
  && Object.entries(result.components).every(([name, value]) =>
    name.endsWith('-rgb') ? /^\d{1,3}, \d{1,3}, \d{1,3}$/.test(value) : /^(?:0|1|0?\.\d{3})$/.test(value))
  && ['background', 'sidebar', 'composer', 'home', 'home-composer', 'polaroid'].every((slot) =>
    result.compositions[`--dream-${slot}-position`] === expectedPositions[slot]
    && result.compositions[`--dream-${slot}-zoom`] === '1')
  && checks.every((check) => check.actual === check.expected)
  && componentChecks.every((check) => check.actual === check.expected);
console.log(JSON.stringify({
  pass,
  ...result,
  checks,
  componentChecks,
  requiredDecorationText,
  forbiddenDecorationText
}, null, 2));
if (!pass) process.exitCode = 1;
