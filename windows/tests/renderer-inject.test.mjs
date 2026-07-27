import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const windowsRoot = path.resolve(here, "..");
const template = await fs.readFile(path.join(windowsRoot, "assets", "renderer-inject.js"), "utf8");
const css = await fs.readFile(path.join(windowsRoot, "assets", "dream-skin.css"), "utf8");
const buildPayload = (config = {}) => template
  .replace("__DREAM_CSS_JSON__", JSON.stringify(".fixture { color: blue; }"))
  .replace("__DREAM_ART_JSON__", JSON.stringify("data:image/png;base64,AA=="))
  .replace("__DREAM_SIDEBAR_ART_JSON__", JSON.stringify("data:image/png;base64,AQ=="))
  .replace("__DREAM_COMPOSER_ART_JSON__", JSON.stringify("data:image/png;base64,Ag=="))
  .replace("__DREAM_HOME_ART_JSON__", JSON.stringify("data:image/png;base64,Aw=="))
  .replace("__DREAM_HOME_COMPOSER_ART_JSON__", JSON.stringify("data:image/png;base64,BA=="))
  .replace("__DREAM_POLAROID_ART_JSON__", JSON.stringify("data:image/png;base64,BQ=="))
  .replace("__DREAM_THEME_JSON__", JSON.stringify(config));
const payload = buildPayload({
  art: { focusX: 0.64, focusY: 0.44 },
  compositions: {
    background: { focusX: 0.64, focusY: 0.44, zoom: 1, fit: "auto", offsetX: 0, offsetY: 0 },
    sidebar: { focusX: 0.25, focusY: 0.7, zoom: 1.6, fit: "cover", offsetX: 0.2, offsetY: -0.1 },
    composer: { focusX: 0.5, focusY: 0.5, zoom: 1.2, fit: "fill", offsetX: 0, offsetY: 0 },
    home: { focusX: 0.75, focusY: 0.35, zoom: 1, fit: "contain", offsetX: -0.2, offsetY: 0.1 },
    polaroid: { focusX: 0.45, focusY: 0.3, zoom: 1.3, fit: "cover", offsetX: 0, offsetY: 0 },
  },
});

assert.match(css, /\.dream-ribbon\s*\{[\s\S]*left:\s*var\(--dream-ribbon-left[\s\S]*top:\s*var\(--dream-ribbon-top[\s\S]*bottom:\s*auto;/,
  "the home ribbon must use a measured hero position instead of the bottom composer lane");
assert.match(css, /@media \(max-width: 1120px\)[\s\S]*\.dream-ribbon\s*\{\s*display:\s*none\s*!important;/,
  "the ribbon must disappear before a narrow home layout can squeeze the typing lane");
assert.match(css, /#codex-dream-skin-chrome\[data-dream-decoration="milky-way"\]\s*:where\([\s\S]*\.dream-ribbon,[\s\S]*\.dream-polaroid[\s\S]*display:\s*none\s*!important/,
  "the Milky Way profile must suppress inherited editorial chrome");
assert.match(css, /data-dream-decoration="milky-way"[\s\S]*\.dream-home[\s\S]*::after\s*\{[\s\S]*content:\s*none\s*!important;[\s\S]*display:\s*none\s*!important;/,
  "the Milky Way home scene must not leave an empty editorial caption rule");
assert.match(css, /content:\s*var\(--dream-home-caption/,
  "the home editorial caption must come from the selected decoration profile");
assert.match(css, /\.dream-polaroid::after\s*\{[\s\S]*content:\s*attr\(data-caption\)/,
  "the observation-card caption must come from the selected decoration profile");
assert.match(template, /--dream-ribbon-top/, "the renderer must publish the measured hero-safe ribbon position");
assert.match(template, /const APP_MENU_ID = "codex-dream-skin-app-menu"/,
  "the renderer must own a reversible translated application-menu surface");
assert.match(template, /typeof window\.__dreamSkinCommand === "function"/,
  "the translated menu must only replace Codex's native menu when the trusted command bridge is available");
for (const label of [
  "新建窗口", "退出登录", "撤销", "粘贴", "设置…",
  "显示 / 隐藏侧边栏", "打开终端", "打开浏览器标签页", "上一个会话", "切换全屏",
  "使用文档", "键盘快捷键", "故障排除", "关于 ChatGPT"
]) {
  assert.match(template, new RegExp(label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")), `missing translated View menu label: ${label}`);
}
assert.match(css, /#codex-dream-skin-app-menu[\s\S]*--dream-component-menus-light-rgb/,
  "the translated application menu must use the configurable light menu material");
assert.match(css, /electron-dark #codex-dream-skin-app-menu[\s\S]*--dream-component-menus-dark-rgb/,
  "the translated application menu must use the configurable dark menu material");

assert.doesNotMatch(
  css,
  /main\.main-surface\s*>\s*header\.app-header-tint\s*\{[^}]*\b(?:position|z-index)\s*:/,
  "The skin must preserve Codex's native fixed header so the side-panel toggle remains reachable.",
);
assert.match(
  css,
  /\.composer-surface-chrome\s+\[class\*="_attachmentsDefault_"\]:not\(:empty\)/,
  "Populated composer attachment queues must receive a dedicated readable glass layer.",
);
assert.match(
  css,
  /--dream-composer-attachment-ink:\s*#173b5a/,
  "Light composer attachment text must use an opaque deep-blue foreground token.",
);
assert.match(
  css,
  /--dream-composer-attachment-ink:\s*#e7f4ff/,
  "Dark composer attachment text must use a readable ice-white foreground token.",
);
assert.match(
  css,
  /\.dream-queued-message-list[\s\S]*--dream-queued-message-ink/,
  "Queued follow-up text must use its own opaque foreground token.",
);
assert.match(
  template,
  /\.vertical-scroll-fade-mask\.hide-scrollbar\[class\*="max-h-\[30dvh\]"\]\[class\*="gap-px"\]\[class\*="px-3"\]/,
  "Queued follow-ups must be distinguished from the activity rail by their exact structural scroll-list traits.",
);
assert.match(
  css,
  /\.dream-queued-message-list[\s\S]{0,240}\[class\*="_markdownContent_"\][\s\S]{0,240}padding:\s*0\s*!important[\s\S]{0,240}background:\s*transparent\s*!important[\s\S]{0,240}line-height:\s*1rem\s*!important/,
  "Queued follow-up Markdown must not inherit the full conversation bubble padding or line height.",
);
assert.match(css, /--dream-task-mask-left:\s*var\(--dream-custom-light-page,\s*\.56\)/);
assert.match(css, /--dream-task-mask-left:\s*var\(--dream-custom-dark-page,\s*\.68\)/);
assert.match(css, /var\(--dream-sidebar-art,\s*var\(--dream-art\)\)/);
assert.match(css, /var\(--dream-composer-art,\s*var\(--dream-art\)\)/);
assert.match(css, /var\(--dream-home-art,\s*var\(--dream-art\)\)/);
assert.match(css, /data-dream-sidebar-background="continuous"\]\s+body::before\s*\{[\s\S]*background-image:\s*var\(--dream-art\)/,
  "continuous sidebar mode must place the main artwork on one viewport-sized plane");
assert.match(css, /data-dream-sidebar-background="continuous"\]\s+aside\.app-shell-left-panel\s*\{[^}]*background:\s*transparent\s*!important;[^}]*backdrop-filter:\s*none\s*!important;/,
  "continuous sidebar mode must make the sidebar fill and backdrop fully transparent");
assert.match(css, /data-dream-sidebar-background="continuous"\]\s+aside\.app-shell-left-panel::before,[\s\S]*aside\.app-shell-left-panel::after\s*\{[^}]*content:\s*none\s*!important;[^}]*display:\s*none\s*!important;/,
  "continuous sidebar mode must remove inherited sidebar pseudo overlays");
assert.match(css, /data-dream-sidebar-background="continuous"\]\s+main\.main-surface\.dream-task-shell\s*\{[\s\S]*background-image:[\s\S]*radial-gradient/,
  "continuous dark task mode must retain the workspace veil without repainting regional artwork");
assert.match(css, /Match the large surfaces at one opacity[\s\S]*data-dream-sidebar-background="continuous"\]\[data-dream-transparency-match="on"\]\s+main\.main-surface,[\s\S]*main\.main-surface\s*>\s*header\.app-header-tint,[\s\S]*background:\s*transparent\s*!important;[\s\S]*backdrop-filter:\s*none\s*!important;/,
  "the match option must give the sidebar and large workspace surfaces the same transparent base");
assert.match(css, /electron-dark\[data-dream-sidebar-background="continuous"\]\[data-dream-transparency-match="on"\]\s+main\.main-surface,[\s\S]*electron-dark\[data-dream-sidebar-background="continuous"\]\[data-dream-transparency-match="on"\]\s+main\.main-surface\s*>\s*header\.app-header-tint,[\s\S]*background:\s*transparent\s*!important;/,
  "the match option must override route-specific dark workspace and header veils");
for (const slot of ["messages", "summaries", "previews", "menus", "workspace", "code", "suggestions"]) {
  assert.match(css, new RegExp(`--dream-component-${slot}-light-(?:rgb|opacity)`), `missing light ${slot} material mapping`);
  assert.match(css, new RegExp(`--dream-component-${slot}-dark-(?:rgb|opacity)`), `missing dark ${slot} material mapping`);
}
assert.match(
  css,
  /\[role="tooltip"\]\[class\*="bg-token-dropdown-background"\]/,
  "Current Codex task-hover previews own the dropdown class on the tooltip root and require a direct selector.",
);
assert.match(
  css,
  /electron-dark :is\([\s\S]{0,260}\[role="tooltip"\]\[class\*="bg-token-dropdown-background"\][\s\S]{0,920}--dream-component-previews-dark-rgb[\s\S]{0,920}color:\s*#f4fbff\s*!important/,
  "Dark task-hover previews must combine the configurable dark surface with a readable light foreground.",
);
assert.match(
  css,
  /html\.codex-dream-skin\.electron-dark aside\.app-shell-left-panel\s*\{[\s\S]*?background-repeat:\s*no-repeat\s*!important;[\s\S]*?--dream-sidebar-size/,
  "Dark sidebar artwork must not fall back to tiled auto sizing after its background shorthand.",
);
assert.match(
  css,
  /html\.codex-dream-skin\.electron-dark main\.main-surface\s*\{[\s\S]*?background-repeat:\s*no-repeat\s*!important;[\s\S]*?--dream-background-size/,
  "Dark main artwork must retain the regional no-repeat composition after route shorthands.",
);
assert.match(
  css,
  /html\.codex-dream-skin\.electron-dark main\.main-surface\.dream-task-shell,[\s\S]*?dream-settings-shell,[\s\S]*?dream-utility-shell\s*\{[\s\S]*?background-repeat:\s*no-repeat\s*!important/,
  "Dark route-specific background shorthands require an equal-specificity no-repeat override.",
);
const darkTaskComposerStart = css.indexOf("html.codex-dream-skin.electron-dark main.dream-task-shell .composer-surface-chrome {");
const darkHomeComposerStart = css.indexOf("html.codex-dream-skin.electron-dark main.dream-home-shell .composer-surface-chrome {", darkTaskComposerStart);
const darkTaskComposerBlock = css.slice(darkTaskComposerStart, darkHomeComposerStart);
assert.ok(darkTaskComposerStart >= 0 && darkHomeComposerStart > darkTaskComposerStart, "dark composer route blocks must remain separate");
assert.doesNotMatch(darkTaskComposerBlock, /--dream-composer-art/, "task composer must render portrait art only through its masked pseudo-layer");
assert.match(darkTaskComposerBlock, /background-size:\s*100% 100%,\s*100% 100%\s*!important/);
assert.match(
  css,
  /main\.dream-task-shell \.composer-surface-chrome::after\s*\{[\s\S]*?background-size:\s*var\(--dream-composer-size,\s*cover\)/,
  "Default task-composer artwork must cover the whole surface instead of ending before the trailing controls.",
);
assert.doesNotMatch(
  css,
  /aside\.app-shell-left-panel\s*\{[^}]*position:\s*relative\s*!important/,
  "The theme must not override Codex's native inline-versus-overlay sidebar positioning.",
);
assert.match(
  css,
  /:is\(aside\.app-shell-left-panel,\s*\.dream-sidebar-surface\)\.dream-sidebar-overlay\s*\{[\s\S]*?border-radius:\s*0 14px 14px 0\s*!important;[\s\S]*?backdrop-filter:\s*blur\(9px\)/,
  "The light overlay sidebar must use a rounded translucent glass drawer.",
);
assert.match(
  css,
  /\.electron-dark :is\(aside\.app-shell-left-panel,\s*\.dream-sidebar-surface\)\.dream-sidebar-overlay\s*\{[\s\S]*?--dream-sidebar-glass-top\) \* \.44[\s\S]*?--dream-sidebar-glass-bottom\) \* \.28[\s\S]*?backdrop-filter:\s*blur\(10px\)/,
  "The dark overlay sidebar must remain translucent and honor the custom sidebar material.",
);
assert.match(
  css,
  /\.dream-sidebar-surface:not\(aside\.app-shell-left-panel\)[\s\S]*?--dream-sidebar-art/,
  "A structurally detected portal drawer must receive the independent sidebar artwork.",
);

function createFixture({
  shellPresent,
  mainPresent = shellPresent,
  sidebarPresent = shellPresent,
  staleSkin = false,
  homePresent = false,
  utilityPresent = false,
  settingsPresent = false,
  sidebarOverlay = false,
  drawerSidebar = false,
  shellAppearance = "dark",
  computedColorScheme = "",
  osAppearance = "light",
  analysisFixture = null,
}) {
  const nodes = new Map();
  const rootClasses = new Set(staleSkin ? ["codex-dream-skin"] : []);
  const rootStyles = new Map(staleSkin ? [["--dream-art", "url(\"blob:stale\")"]] : []);
  const revokedUrls = [];
  const observers = [];
  let objectUrlCount = 0;
  let hasMain = mainPresent;
  let hasSidebar = sidebarPresent;
  let root;

  const queueRootClassMutation = () => {
    for (const observer of observers) {
      if (observer.target !== root || !observer.options?.attributes) continue;
      if (observer.options.attributeFilter && !observer.options.attributeFilter.includes("class")) continue;
      observer.records.push({ type: "attributes", attributeName: "class", target: root });
    }
  };
  const makeClassList = (classes = new Set(), onMutation = () => {}) => ({
    add(...values) {
      let changed = false;
      for (const value of values) {
        if (!classes.has(value)) { classes.add(value); changed = true; }
      }
      if (changed) onMutation();
    },
    remove(...values) {
      let changed = false;
      for (const value of values) changed = classes.delete(value) || changed;
      if (changed) onMutation();
    },
    toggle(value, enabled) {
      const changed = enabled ? !classes.has(value) : classes.has(value);
      if (enabled) classes.add(value);
      else classes.delete(value);
      if (changed) onMutation();
    },
    contains(value) { return classes.has(value); },
  });

  root = {
    className: shellAppearance,
    classList: makeClassList(rootClasses, queueRootClassMutation),
    dataset: {},
    getAttribute() { return null; },
    getBoundingClientRect() {
      return { left: 0, right: 1280, top: 0, bottom: 820, width: 1280, height: 820 };
    },
    style: {
      setProperty(key, value) { rootStyles.set(key, value); },
      removeProperty(key) { rootStyles.delete(key); },
    },
    appendChild(node) {
      node.parentElement = root;
      nodes.set(node.id, node);
    },
  };
  const body = {
    className: "",
    getAttribute() { return null; },
    appendChild(node) {
      node.parentElement = body;
      nodes.set(node.id, node);
    },
  };
  const shellClasses = new Set();
  const shellMain = {
    classList: makeClassList(shellClasses),
    querySelector(selector) {
      if (settingsPresent && selector === ".main-surface") return settingsSurface;
      if (utilityPresent && selector.includes('bg-token-main-surface-primary')) return utilityNode;
      return null;
    },
    querySelectorAll(selector) {
      if (settingsPresent && selector === '[role="switch"]') return settingsSwitches;
      if (settingsPresent && selector === '[role="combobox"]') return settingsSelects;
      return [];
    },
    getBoundingClientRect() {
      const left = sidebarOverlay ? 0 : 290;
      return { left, right: left + 990, top: 36, bottom: 820, width: 990, height: 784 };
    },
  };
  const routeClasses = new Set();
  const utilityClasses = new Set();
  const utilityNode = { classList: makeClassList(utilityClasses) };
  const settingsSurface = { classList: makeClassList(new Set(["main-surface"])) };
  const settingsSwitches = settingsPresent ? [{}, {}, {}] : [];
  const settingsSelects = settingsPresent ? [{}] : [];
  const sidebarClasses = new Set();
  const settingsSidebar = {
    classList: makeClassList(sidebarClasses),
    parentElement: body,
    querySelector() { return null; },
    querySelectorAll() { return []; },
    getBoundingClientRect() {
      return { left: 0, right: 275, top: 36, bottom: 820, width: 275, height: 784 };
    },
  };
  const sidebarNavigation = { parentElement: settingsSidebar };
  const routeMain = {
    classList: makeClassList(routeClasses),
    querySelectorAll(selector) {
      if (selector === '[class*="_homeUtilityBar_"]' && utilityPresent) return [utilityNode];
      return [];
    },
  };
  const staleHome = { classList: makeClassList(new Set(["dream-home"])) };
  const staleShell = { classList: makeClassList(new Set(["dream-home-shell"])) };

  const createElement = (tagName) => {
    if (tagName === "canvas" && analysisFixture) {
      return {
        width: 0,
        height: 0,
        getContext() {
          return {
            drawImage() {},
            getImageData() { return { data: analysisFixture.pixels }; },
          };
        },
      };
    }
    return {
      id: "",
      dataset: {},
      style: {},
      classList: makeClassList(),
      parentElement: null,
      textContent: "",
      innerHTML: "",
      setAttribute() {},
      remove() { nodes.delete(this.id); },
    };
  };
  if (staleSkin) {
    const style = createElement();
    style.id = "codex-dream-skin-style";
    nodes.set(style.id, style);
    const chrome = createElement();
    chrome.id = "codex-dream-skin-chrome";
    nodes.set(chrome.id, chrome);
  }

  const document = {
    documentElement: root,
    head: root,
    body,
    addEventListener() {},
    createElement,
    getElementById(id) { return nodes.get(id) ?? null; },
    querySelector(selector) {
      if (selector === "main.main-surface") return hasMain ? shellMain : null;
      if (selector === "main") return hasMain ? shellMain : null;
      if (selector === "aside.app-shell-left-panel") return hasSidebar && !drawerSidebar ? settingsSidebar : null;
      if (selector === "nav.sidebar-foreground-muted") return hasSidebar ? sidebarNavigation : null;
      if (selector === '[role="main"]:has([data-testid="home-icon"])') {
        return hasMain && homePresent ? routeMain : null;
      }
      if (selector === '[role="main"]') return hasMain ? routeMain : null;
      return null;
    },
    querySelectorAll(selector) {
      if (selector === '[role="main"]') return hasMain ? [routeMain] : [];
      if (selector === ".dream-home") return routeClasses.has("dream-home") ? [routeMain] : [];
      if (selector === ".dream-home-shell") return shellClasses.has("dream-home-shell") ? [shellMain] : [];
      if (selector === ".dream-settings-shell") return shellClasses.has("dream-settings-shell") ? [shellMain] : [];
      if (selector === ".dream-settings-sidebar") return sidebarClasses.has("dream-settings-sidebar") ? [settingsSidebar] : [];
      if (selector === ".dream-sidebar-overlay") return sidebarClasses.has("dream-sidebar-overlay") ? [settingsSidebar] : [];
      if (selector === ".dream-sidebar-surface") return sidebarClasses.has("dream-sidebar-surface") ? [settingsSidebar] : [];
      if (selector === ".dream-utility-shell") return shellClasses.has("dream-utility-shell") ? [shellMain] : [];
      if (selector === ".dream-task-shell") return shellClasses.has("dream-task-shell") ? [shellMain] : [];
      if (selector === '[class*="dream-suggestion-"]' || selector === "diffs-container") return [];
      if (!staleSkin) return [];
      if (selector === ".dream-home") return [staleHome];
      if (selector === ".dream-home-shell") return [staleShell];
      return [];
    },
  };
  const context = {
    window: {
      matchMedia() { return { matches: osAppearance === "dark" }; },
    },
    document,
    MutationObserver: class {
      constructor(callback) {
        this.callback = callback;
        this.records = [];
        this.target = null;
        this.options = null;
        observers.push(this);
      }
      observe(target, options = {}) {
        this.target = target;
        this.options = options;
      }
      disconnect() {
        this.target = null;
        this.records = [];
      }
      takeRecords() {
        const records = this.records;
        this.records = [];
        return records;
      }
    },
    URL: {
      createObjectURL() { objectUrlCount += 1; return `blob:fixture-${objectUrlCount}`; },
      revokeObjectURL(value) { revokedUrls.push(value); },
    },
    Blob,
    AbortController,
    Uint8Array,
    atob,
    setInterval: () => 1,
    clearInterval: () => {},
    setTimeout: () => 2,
    clearTimeout: () => {},
    getComputedStyle() { return { colorScheme: computedColorScheme }; },
  };
  if (analysisFixture) {
    context.Image = class {
      naturalWidth = analysisFixture.naturalWidth;
      naturalHeight = analysisFixture.naturalHeight;
      set src(_) { this.onload(); }
    };
  }

  return {
    context,
    nodes,
    observers,
    rootClasses,
    rootStyles,
    revokedUrls,
    routeClasses,
    shellClasses,
    sidebarClasses,
    utilityClasses,
    setShellPresent(value) {
      hasMain = value;
      hasSidebar = value;
    },
    setSidebarPresent(value) { hasSidebar = value; },
    setMainPresent(value) { hasMain = value; },
  };
}

const main = createFixture({ shellPresent: true });
const mainResult = vm.runInNewContext(payload, main.context);
assert.equal(mainResult.installed, true);
assert.equal(mainResult.version, "3.10.0");
assert.equal(main.rootClasses.has("codex-dream-skin"), true);
assert.equal(main.context.document.documentElement.dataset.dreamSidebarBackground, "independent");
assert.equal(main.context.document.documentElement.dataset.dreamTransparencyMatch, "off");
assert.equal(main.rootStyles.get("--dream-art"), 'url("blob:fixture-1")');
assert.equal(main.rootStyles.get("--dream-sidebar-art"), 'url("blob:fixture-2")');
assert.equal(main.rootStyles.get("--dream-composer-art"), 'url("blob:fixture-3")');
assert.equal(main.rootStyles.get("--dream-home-art"), 'url("blob:fixture-4")');
assert.equal(main.rootStyles.get("--dream-home-composer-art"), 'url("blob:fixture-5")');
assert.equal(main.rootStyles.get("--dream-polaroid-art"), 'url("blob:fixture-6")');
assert.equal(main.rootStyles.get("--dream-background-position"), "64.00% 44.00%");
assert.equal(main.rootStyles.has("--dream-background-size"), false);
assert.equal(main.rootStyles.get("--dream-sidebar-position"), "30.00% 67.50%");
assert.equal(main.rootStyles.get("--dream-sidebar-size"), "160.00% auto");
assert.equal(main.rootStyles.get("--dream-composer-size"), "120.00% 120.00%");
assert.equal(main.rootStyles.get("--dream-home-position"), "70.00% 37.50%");
assert.equal(main.rootStyles.get("--dream-home-size"), "contain");
assert.equal(main.nodes.has("codex-dream-skin-style"), true);
assert.equal(main.nodes.has("codex-dream-skin-chrome"), true);
assert.equal(main.shellClasses.has("dream-task-shell"), true);
assert.equal(main.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup(), true);
assert.equal(main.rootClasses.has("codex-dream-skin"), false);
assert.equal(main.rootStyles.has("--dream-sidebar-position"), false);
assert.equal(main.nodes.has("codex-dream-skin-style"), false);
assert.equal(main.nodes.has("codex-dream-skin-chrome"), false);
assert.equal(main.context.document.documentElement.dataset.dreamSidebarBackground, undefined);
assert.equal(main.context.document.documentElement.dataset.dreamTransparencyMatch, undefined);
assert.deepEqual(main.revokedUrls, ["blob:fixture-1", "blob:fixture-2", "blob:fixture-3", "blob:fixture-4", "blob:fixture-5", "blob:fixture-6"]);

const milkyDecorations = createFixture({ shellPresent: true, homePresent: true });
const milkyResult = vm.runInNewContext(buildPayload({
  decorations: { profile: "milky-way" },
}), milkyDecorations.context);
const milkyChrome = milkyDecorations.nodes.get("codex-dream-skin-chrome");
assert.equal(milkyResult.version, "3.10.0");
assert.equal(milkyDecorations.context.document.documentElement.dataset.dreamDecoration, "milky-way");
assert.equal(milkyChrome.dataset.dreamDecoration, "milky-way");
assert.equal(milkyChrome.innerHTML, "");
assert.equal(milkyDecorations.rootStyles.get("--dream-home-caption"), '""');
assert.equal(milkyDecorations.rootStyles.get("--dream-mode-badge"), '""');
assert.equal(milkyDecorations.rootStyles.get("--dream-profile-badge"), '""');
assert.equal(milkyDecorations.context.window.__CODEX_DREAM_SKIN_STATE__.decorationProfile, "milky-way");
assert.equal(milkyDecorations.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup(), true);
assert.equal(milkyDecorations.context.document.documentElement.dataset.dreamDecoration, undefined);
assert.equal(milkyDecorations.rootStyles.has("--dream-home-caption"), false);

const continuousSidebar = createFixture({ shellPresent: true });
const continuousResult = vm.runInNewContext(buildPayload({
  surfaces: { sidebarBackground: "continuous", matchWorkspaceTransparency: true },
  imageMetadata: { background: { width: 1493, height: 1053 } },
  compositions: {
    background: { focusX: .52, focusY: .43, zoom: 1, fit: "cover", offsetX: 0, offsetY: 0 },
  },
}), continuousSidebar.context);
assert.equal(continuousResult.version, "3.10.0");
assert.equal(continuousSidebar.context.document.documentElement.dataset.dreamSidebarBackground, "continuous");
assert.equal(continuousSidebar.context.document.documentElement.dataset.dreamTransparencyMatch, "on");
assert.equal(continuousSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.sidebarBackgroundMode, "continuous");
assert.equal(continuousSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.matchWorkspaceTransparency, true);
const [continuousWidth, continuousHeight] = continuousSidebar.rootStyles.get("--dream-background-size")
  .split(" ").map(value => Number.parseFloat(value));
assert.ok(Math.abs(continuousWidth - 1280) < .02, "continuous mode must size the artwork against the complete viewport");
assert.ok(Math.abs(continuousWidth / continuousHeight - 1493 / 1053) < .0001,
  "continuous viewport sizing must preserve the source aspect ratio");
assert.equal(continuousSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup(), true);
assert.equal(continuousSidebar.context.document.documentElement.dataset.dreamSidebarBackground, undefined);
assert.equal(continuousSidebar.context.document.documentElement.dataset.dreamTransparencyMatch, undefined);

const exactCrop = createFixture({ shellPresent: true });
vm.runInNewContext(buildPayload({
  imageMetadata: { background: { width: 1493, height: 1053 } },
  compositions: {
    background: { focusX: .64, focusY: .44, zoom: .5, fit: "cover", offsetX: 0, offsetY: 0 },
  },
}), exactCrop.context);
const [exactWidth, exactHeight] = exactCrop.rootStyles.get("--dream-background-size")
  .split(" ").map(value => Number.parseFloat(value));
assert.ok(Math.abs(exactHeight - 784) < .02, "cover must still fill the live target when stored zoom is below 1");
assert.ok(Math.abs(exactWidth / exactHeight - 1493 / 1053) < .0001, "pixel sizing must preserve the source aspect ratio");
assert.equal(exactCrop.rootStyles.get("--dream-background-position"), "64.00% 44.00%");
exactCrop.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup();

const materials = createFixture({ shellPresent: true });
const materialsPayload = buildPayload({
  materials: {
    light: { page: 0.51, sidebar: 0.52, composer: 0.53, card: 0.14 },
    dark: { page: 0.71, sidebar: 0.72, composer: 0.73, card: 0.44 },
    components: {
      messages: { light: { color: "#123456", opacity: 0.21 }, dark: { color: "#ABCDEF", opacity: 0.61 } },
      previews: { light: { color: "#203040", opacity: 0.71 }, dark: { color: "#061728", opacity: 0.88 } },
      code: { light: { color: "#102030", opacity: 0.12 }, dark: { color: "#405060", opacity: 0.32 } },
    },
  },
});
vm.runInNewContext(materialsPayload, materials.context);
assert.equal(materials.rootStyles.get("--dream-custom-light-page"), "0.510");
assert.equal(materials.rootStyles.get("--dream-custom-light-sidebar"), "0.520");
assert.equal(materials.rootStyles.get("--dream-custom-light-composer"), "0.530");
assert.equal(materials.rootStyles.get("--dream-custom-light-card"), "0.140");
assert.equal(materials.rootStyles.get("--dream-custom-dark-page"), "0.710");
assert.equal(materials.rootStyles.get("--dream-custom-dark-sidebar"), "0.720");
assert.equal(materials.rootStyles.get("--dream-custom-dark-composer"), "0.730");
assert.equal(materials.rootStyles.get("--dream-custom-dark-card"), "0.440");
assert.equal(materials.rootStyles.get("--dream-component-messages-light-rgb"), "18, 52, 86");
assert.equal(materials.rootStyles.get("--dream-component-messages-light-opacity"), "0.210");
assert.equal(materials.rootStyles.get("--dream-component-messages-dark-rgb"), "171, 205, 239");
assert.equal(materials.rootStyles.get("--dream-component-messages-dark-opacity"), "0.610");
assert.equal(materials.rootStyles.get("--dream-component-previews-light-rgb"), "32, 48, 64");
assert.equal(materials.rootStyles.get("--dream-component-previews-dark-opacity"), "0.880");
assert.equal(materials.rootStyles.get("--dream-component-code-light-rgb"), "16, 32, 48");
assert.equal(materials.rootStyles.get("--dream-component-code-dark-opacity"), "0.320");
materials.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup();
assert.equal(materials.rootStyles.has("--dream-custom-light-page"), false);
assert.equal(materials.rootStyles.has("--dream-custom-dark-card"), false);
assert.equal(materials.rootStyles.has("--dream-component-messages-light-rgb"), false);
assert.equal(materials.rootStyles.has("--dream-component-previews-dark-opacity"), false);
assert.equal(materials.rootStyles.has("--dream-component-code-dark-opacity"), false);

const reinjected = createFixture({ shellPresent: true });
vm.runInNewContext(payload, reinjected.context);
const firstState = reinjected.context.window.__CODEX_DREAM_SKIN_STATE__;
vm.runInNewContext(payload, reinjected.context);
const secondState = reinjected.context.window.__CODEX_DREAM_SKIN_STATE__;
assert.notEqual(secondState, firstState);
assert.equal(secondState.artUrl, "blob:fixture-7");
assert.equal(reinjected.rootStyles.get("--dream-art"), 'url("blob:fixture-7")');
assert.deepEqual(reinjected.revokedUrls, ["blob:fixture-1", "blob:fixture-2", "blob:fixture-3", "blob:fixture-4", "blob:fixture-5", "blob:fixture-6"]);
assert.equal(secondState.cleanup(), true);
assert.deepEqual(reinjected.revokedUrls, [
  "blob:fixture-1", "blob:fixture-2", "blob:fixture-3", "blob:fixture-4", "blob:fixture-5", "blob:fixture-6",
  "blob:fixture-7", "blob:fixture-8", "blob:fixture-9", "blob:fixture-10", "blob:fixture-11", "blob:fixture-12",
]);

const auxiliary = createFixture({ shellPresent: false, staleSkin: true });
const auxiliaryResult = vm.runInNewContext(payload, auxiliary.context);
assert.equal(auxiliaryResult.installed, true);
assert.equal(auxiliary.rootClasses.has("codex-dream-skin"), false);
assert.equal(auxiliary.rootStyles.has("--dream-art"), false);
assert.equal(auxiliary.nodes.has("codex-dream-skin-style"), false);
assert.equal(auxiliary.nodes.has("codex-dream-skin-chrome"), false);
auxiliary.setShellPresent(true);
auxiliary.context.window.__CODEX_DREAM_SKIN_STATE__.ensure();
assert.equal(auxiliary.rootClasses.has("codex-dream-skin"), true);
assert.equal(auxiliary.nodes.has("codex-dream-skin-style"), true);
assert.equal(auxiliary.nodes.has("codex-dream-skin-chrome"), true);

const collapsedSidebar = createFixture({ shellPresent: true, sidebarPresent: false });
vm.runInNewContext(payload, collapsedSidebar.context);
assert.equal(collapsedSidebar.rootClasses.has("codex-dream-skin"), true);
assert.equal(collapsedSidebar.shellClasses.has("dream-task-shell"), true);
collapsedSidebar.setMainPresent(false);
collapsedSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.ensure();
assert.equal(collapsedSidebar.rootClasses.has("codex-dream-skin"), false);
assert.equal(collapsedSidebar.nodes.has("codex-dream-skin-style"), false);

const overlaySidebar = createFixture({ shellPresent: true, sidebarOverlay: true });
vm.runInNewContext(payload, overlaySidebar.context);
assert.equal(overlaySidebar.sidebarClasses.has("dream-sidebar-overlay"), true);
assert.equal(overlaySidebar.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup(), true);
assert.equal(overlaySidebar.sidebarClasses.has("dream-sidebar-overlay"), false);

const portalDrawer = createFixture({ shellPresent: true, drawerSidebar: true });
vm.runInNewContext(payload, portalDrawer.context);
assert.equal(portalDrawer.sidebarClasses.has("dream-sidebar-surface"), true);
assert.equal(
  portalDrawer.sidebarClasses.has("dream-sidebar-overlay"),
  true,
  "A portal-hosted structural navigation drawer must receive the translucent overlay treatment.",
);

const sidebarlessSettings = createFixture({
  shellPresent: true,
  sidebarPresent: false,
  settingsPresent: true,
});
vm.runInNewContext(payload, sidebarlessSettings.context);
assert.equal(
  sidebarlessSettings.shellClasses.has("dream-settings-shell"),
  true,
  "Settings reached from a destroyed hover drawer must retain the themed settings canvas.",
);
assert.equal(
  sidebarlessSettings.shellClasses.has("dream-task-shell"),
  false,
  "A sidebarless settings route must not fall back to task styling.",
);
assert.match(
  template,
  /settingsSidebarRecovery[\s\S]*?button\[aria-haspopup="menu"\][\s\S]*?Ctrl\\s\*\\\+,/,
  "Sidebarless settings must recover through the native sidebar and native shortcut-bearing settings menu item.",
);
portalDrawer.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup();

const revealedSidebar = createFixture({ shellPresent: true });
vm.runInNewContext(payload, revealedSidebar.context);
assert.equal(revealedSidebar.sidebarClasses.has("dream-sidebar-overlay"), false);
revealedSidebar.setSidebarPresent(false);
revealedSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.ensure();
revealedSidebar.setSidebarPresent(true);
revealedSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.ensure();
assert.equal(
  revealedSidebar.sidebarClasses.has("dream-sidebar-overlay"),
  true,
  "A sidebar revealed after being collapsed must retain the translucent drawer treatment even when it shifts main content.",
);
vm.runInNewContext(payload, revealedSidebar.context);
assert.equal(
  revealedSidebar.sidebarClasses.has("dream-sidebar-overlay"),
  true,
  "The revealed-sidebar lifecycle must survive renderer hot reloads.",
);
revealedSidebar.context.window.__CODEX_DREAM_SKIN_STATE__.cleanup();

const home = createFixture({ shellPresent: true, homePresent: true });
vm.runInNewContext(payload, home.context);
assert.equal(home.routeClasses.has("dream-home"), true);
assert.equal(home.shellClasses.has("dream-home-shell"), true);
assert.equal(home.shellClasses.has("dream-task-shell"), false);

const utility = createFixture({ shellPresent: true, utilityPresent: true });
vm.runInNewContext(payload, utility.context);
assert.equal(utility.shellClasses.has("dream-utility-shell"), true);
assert.equal(utility.shellClasses.has("dream-task-shell"), false);

console.log("PASS: renderer preserves task/home/utility routing, collapsed-sidebar continuity, and transparent auxiliary windows.");
