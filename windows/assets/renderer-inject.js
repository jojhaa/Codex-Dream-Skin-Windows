(() => {
  try {
    return ((cssText, artDataUrl, sidebarArtDataUrl, composerArtDataUrl, homeArtDataUrl, homeComposerArtDataUrl, polaroidArtDataUrl, theme) => {
  const STATE_KEY = "__CODEX_DREAM_SKIN_STATE__";
  const STYLE_ID = "codex-dream-skin-style";
  const CHROME_ID = "codex-dream-skin-chrome";
  const APP_MENU_ID = "codex-dream-skin-app-menu";
  const DIFF_SHADOW_STYLE_ID = "codex-dream-skin-diff-shadow-style";
  const STYLE_SCHEMA = "69";
  const diffShadowCss = `
    :host {
      --vscode-editor-background: transparent !important;
      --vscode-diffEditor-unchangedRegionBackground: transparent !important;
      --vscode-diffEditor-unchangedCodeBackground: transparent !important;
    }

    [data-diffs-header],
    :is([data-diff], [data-file]) {
      --codex-diffs-surface: rgba(var(--dream-component-code-light-rgb, 250, 253, 252), var(--dream-component-code-light-opacity, var(--dream-diff-inner-glass, .08))) !important;
      --codex-diffs-context-surface: rgba(247, 251, 250, .06) !important;
      --codex-diffs-separator-surface: rgba(21, 87, 176, .05) !important;
      --codex-diffs-header-surface: rgba(229, 243, 246, .08) !important;
      --codex-diffs-context-number: rgba(60, 88, 112, .10) !important;
      --codex-diffs-addition-number: rgba(25, 123, 99, .14) !important;
      --codex-diffs-deletion-number: rgba(197, 39, 70, .13) !important;
      --codex-diffs-addition-hover: rgba(25, 123, 99, .16) !important;
      --codex-diffs-deletion-hover: rgba(197, 39, 70, .15) !important;
      --diffs-bg: rgba(var(--dream-component-code-light-rgb, 250, 253, 252), var(--dream-component-code-light-opacity, var(--dream-diff-inner-glass, .08))) !important;
      --diffs-bg-buffer: rgba(236, 246, 247, .08) !important;
      --diffs-bg-separator: rgba(21, 87, 176, .03) !important;
      --diffs-bg-context-override: rgba(247, 251, 250, .06) !important;
      --diffs-bg-separator-override: rgba(21, 87, 176, .05) !important;
      background-color: rgba(var(--dream-component-code-light-rgb, 250, 253, 252), var(--dream-component-code-light-opacity, var(--dream-diff-inner-glass, .08))) !important;
    }

    :is([data-diff], [data-file]) :is([data-code], [data-gutter], [data-content]) {
      background-color: transparent;
    }

    :host-context(html.electron-dark) [data-diffs-header],
    :host-context(html.electron-dark) :is([data-diff], [data-file]) {
      --codex-diffs-surface: rgba(var(--dream-component-code-dark-rgb, 7, 27, 46), var(--dream-component-code-dark-opacity, var(--dream-diff-inner-glass, .16))) !important;
      --codex-diffs-context-surface: rgba(7, 27, 46, .14) !important;
      --codex-diffs-separator-surface: rgba(103, 185, 255, .08) !important;
      --codex-diffs-header-surface: rgba(15, 56, 89, .16) !important;
      --codex-diffs-context-number: rgba(183, 204, 224, .14) !important;
      --codex-diffs-addition-number: rgba(103, 219, 186, .18) !important;
      --codex-diffs-deletion-number: rgba(255, 124, 154, .17) !important;
      --codex-diffs-addition-hover: rgba(103, 219, 186, .20) !important;
      --codex-diffs-deletion-hover: rgba(255, 124, 154, .19) !important;
      --diffs-bg: rgba(var(--dream-component-code-dark-rgb, 7, 27, 46), var(--dream-component-code-dark-opacity, var(--dream-diff-inner-glass, .16))) !important;
      --diffs-bg-buffer: rgba(10, 39, 64, .16) !important;
      --diffs-bg-separator: rgba(103, 185, 255, .07) !important;
      --diffs-bg-context-override: rgba(7, 27, 46, .14) !important;
      --diffs-bg-separator-override: rgba(103, 185, 255, .08) !important;
      background-color: rgba(var(--dream-component-code-dark-rgb, 7, 27, 46), var(--dream-component-code-dark-opacity, var(--dream-diff-inner-glass, .16))) !important;
      color: var(--dream-ink, #e9f5ff) !important;
    }
  `;
  window.__CODEX_DREAM_SKIN_DISABLED__ = false;

  const previous = window[STATE_KEY];
  if (previous?.observer) previous.observer.disconnect();
  if (previous?.regionResizeObserver) previous.regionResizeObserver.disconnect();
  if (previous?.timer) clearInterval(previous.timer);
  if (previous?.scheduler?.timeout) clearTimeout(previous.scheduler.timeout);
  previous?.menuAbortController?.abort();
  document.getElementById(APP_MENU_ID)?.remove();
  const unlockedTaskItems = previous?.unlockedTaskItems || new Set();
  const dataUrlToObjectUrl = (dataUrl) => {
    const comma = dataUrl.indexOf(",");
    const binary = atob(dataUrl.slice(comma + 1));
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
    const mime = dataUrl.slice(5, comma).split(";")[0] || "application/octet-stream";
    return URL.createObjectURL(new Blob([bytes], { type: mime }));
  };
  const urlCache = new Map();
  const sidebarLifecycle = previous?.sidebarLifecycle || {
    wasCollapsed: !document.querySelector("aside.app-shell-left-panel") &&
      !document.querySelector("nav.sidebar-foreground-muted"),
    revealed: false
  };
  const settingsSidebarRecovery = previous?.settingsSidebarRecovery || {
    attempted: false,
    pending: false,
    timers: new Set()
  };
  const objectUrlFor = (dataUrl) => {
    if (!urlCache.has(dataUrl)) urlCache.set(dataUrl, dataUrlToObjectUrl(dataUrl));
    return urlCache.get(dataUrl);
  };
  const artUrls = {
    background: objectUrlFor(artDataUrl),
    sidebar: objectUrlFor(sidebarArtDataUrl || artDataUrl),
    composer: objectUrlFor(composerArtDataUrl || artDataUrl),
    home: objectUrlFor(homeArtDataUrl || artDataUrl),
    homeComposer: objectUrlFor(homeComposerArtDataUrl || composerArtDataUrl || artDataUrl),
    polaroid: objectUrlFor(polaroidArtDataUrl || homeArtDataUrl || artDataUrl)
  };
  const artUrl = artUrls.background;
  const decorationProfiles = Object.freeze({
    "kanna-blue": {
      id: "kanna-blue",
      enabled: true,
      note: "01",
      brand: "桥本环奈 · 蓝色瞬间",
      subtitle: "KANNA HASHIMOTO / CODEX EDITION",
      signature: "Kanna / 環奈",
      task: "03 / KANNA BLUE",
      taskEdition: "TASK EDITION",
      ribbonIndex: "01",
      ribbon: "BLUE MOMENT",
      ribbonMark: "●",
      polaroid: "BLUE MOMENT · 01",
      homeCaption: "HASHIMOTO KANNA · BLUE MOMENT",
      modeBadge: "KANNA  01",
      profileBadge: "KANNA BLUE"
    },
    "milky-way": {
      id: "milky-way",
      enabled: false,
      note: "",
      brand: "",
      subtitle: "",
      signature: "",
      task: "",
      taskEdition: "",
      ribbonIndex: "",
      ribbon: "",
      ribbonMark: "",
      polaroid: "",
      homeCaption: "",
      modeBadge: "",
      profileBadge: ""
    },
    minimal: {
      id: "minimal",
      enabled: false,
      note: "",
      brand: "",
      subtitle: "",
      signature: "",
      task: "",
      taskEdition: "",
      ribbonIndex: "",
      ribbon: "",
      ribbonMark: "",
      polaroid: "",
      homeCaption: "",
      modeBadge: "",
      profileBadge: ""
    }
  });
  const requestedDecorationProfile = theme?.decorations?.profile;
  const decorationProfile = decorationProfiles[requestedDecorationProfile] || decorationProfiles["kanna-blue"];
  const sidebarBackgroundMode = theme?.surfaces?.sidebarBackground === "continuous"
    ? "continuous"
    : "independent";
  const matchWorkspaceTransparency = theme?.surfaces?.matchWorkspaceTransparency === true;
  const previousUrls = previous?.artUrls ? Object.values(previous.artUrls) : previous?.artUrl ? [previous.artUrl] : [];
  for (const url of new Set(previousUrls)) URL.revokeObjectURL(url);
  const existingStyle = document.getElementById(STYLE_ID);
  if (existingStyle) {
    try {
      existingStyle.textContent = cssText;
    } catch (error) {
      throw new Error(`Dream skin stylesheet update failed: ${error?.message || error}`);
    }
    existingStyle.dataset.dreamVersion = STYLE_SCHEMA;
  }
  const componentSlots = ["messages", "summaries", "previews", "menus", "workspace", "code", "suggestions"];
  const menuAbortController = new AbortController();
  const applicationMenus = {
    file: {
      labels: ["文件", "File"],
      ariaLabel: "文件菜单",
      items: [
        { command: "newWindow", label: "新建窗口", shortcut: "Ctrl+Shift+N" },
        { command: "newChat", label: "新建会话", shortcut: "Ctrl+N" },
        { command: "openFolder", label: "打开文件夹…", shortcut: "Ctrl+O" },
        { separator: true },
        { command: "close", label: "关闭", shortcut: "Ctrl+W" },
        { separator: true },
        { command: "logout", label: "退出登录", shortcut: "" },
        { command: "exit", label: "退出 Codex", shortcut: "Ctrl+Q" }
      ]
    },
    edit: {
      labels: ["编辑", "Edit"],
      ariaLabel: "编辑菜单",
      items: [
        { command: "undo", label: "撤销", shortcut: "Ctrl+Z" },
        { command: "redo", label: "重做", shortcut: "Ctrl+Y" },
        { separator: true },
        { command: "cut", label: "剪切", shortcut: "Ctrl+X" },
        { command: "copy", label: "复制", shortcut: "Ctrl+C" },
        { command: "paste", label: "粘贴", shortcut: "Ctrl+V" },
        { command: "delete", label: "删除", shortcut: "Delete" },
        { command: "selectAll", label: "全选", shortcut: "Ctrl+A" },
        { separator: true },
        { command: "settings", label: "设置…", shortcut: "Ctrl+," }
      ]
    },
    view: {
      labels: ["视图", "View"],
      ariaLabel: "视图菜单",
      items: [
        { command: "toggleSidebar", label: "显示 / 隐藏侧边栏", shortcut: "Ctrl+B" },
        { command: "toggleBottomPanel", label: "显示 / 隐藏底部面板", shortcut: "Ctrl+J" },
        { command: "togglePinnedSummary", label: "显示 / 隐藏置顶摘要", shortcut: "" },
        { separator: true },
        { command: "openTerminal", label: "打开终端", shortcut: "Ctrl+`" },
        { command: "toggleFileTree", label: "显示 / 隐藏文件树", shortcut: "Ctrl+Shift+E" },
        { command: "toggleReviewPanel", label: "显示 / 隐藏审查面板", shortcut: "Ctrl+Alt+B" },
        { separator: true },
        { command: "openBrowserTab", label: "打开浏览器标签页", shortcut: "Ctrl+T" },
        { command: "focusBrowserAddressBar", label: "聚焦浏览器地址栏", shortcut: "Ctrl+L" },
        { command: "reloadBrowserPage", label: "重新加载浏览器页面", shortcut: "Ctrl+R" },
        { separator: true },
        { command: "find", label: "查找", shortcut: "Ctrl+F" },
        { separator: true },
        { command: "previousChat", label: "上一个会话", shortcut: "Ctrl+Shift+[" },
        { command: "nextChat", label: "下一个会话", shortcut: "Ctrl+Shift+]" },
        { command: "back", label: "后退", shortcut: "Ctrl+[" },
        { command: "forward", label: "前进", shortcut: "Ctrl+]" },
        { separator: true },
        { command: "zoomIn", label: "放大", shortcut: "Ctrl+=" },
        { command: "zoomOut", label: "缩小", shortcut: "Ctrl+-" },
        { command: "actualSize", label: "实际大小", shortcut: "Ctrl+0" },
        { separator: true },
        { command: "toggleFullScreen", label: "切换全屏", shortcut: "F11" }
      ]
    },
    help: {
      labels: ["帮助", "Help"],
      ariaLabel: "帮助菜单",
      items: [
        { command: "documentation", label: "使用文档", shortcut: "" },
        { command: "keyboardShortcuts", label: "键盘快捷键", shortcut: "Ctrl+/" },
        { command: "whatsNew", label: "新增功能", shortcut: "" },
        { command: "troubleshooting", label: "故障排除", shortcut: "" },
        { command: "systemStatus", label: "系统状态", shortcut: "" },
        { command: "sendFeedback", label: "发送反馈", shortcut: "" },
        { separator: true },
        { command: "startPerformanceTrace", label: "开始性能跟踪", shortcut: "" },
        { separator: true },
        { command: "about", label: "关于 ChatGPT", shortcut: "" }
      ]
    }
  };
  const closeApplicationMenu = () => document.getElementById(APP_MENU_ID)?.remove();
  const invokeApplicationMenuCommand = (command) => {
    if (typeof window.__dreamSkinCommand !== "function") return false;
    try {
      window.__dreamSkinCommand(JSON.stringify({ command }));
      return true;
    } catch {
      return false;
    }
  };
  const openApplicationMenu = (anchor, definition) => {
    closeApplicationMenu();
    const menu = document.createElement("div");
    menu.id = APP_MENU_ID;
    menu.className = "dream-application-menu";
    menu.setAttribute("role", "menu");
    menu.setAttribute("aria-label", definition.ariaLabel);
    menu.dataset.dreamMenu = definition.ariaLabel;
    for (const item of definition.items) {
      if (item.separator) {
        const separator = document.createElement("div");
        separator.className = "dream-application-menu-separator";
        separator.setAttribute("role", "separator");
        menu.appendChild(separator);
        continue;
      }
      const button = document.createElement("button");
      button.type = "button";
      button.className = "dream-application-menu-item";
      button.setAttribute("role", "menuitem");
      button.dataset.dreamCommand = item.command;
      const label = document.createElement("span");
      label.textContent = item.label;
      const shortcut = document.createElement("kbd");
      shortcut.textContent = item.shortcut;
      button.append(label, shortcut);
      button.addEventListener("click", () => {
        if (invokeApplicationMenuCommand(item.command)) closeApplicationMenu();
      });
      menu.appendChild(button);
    }
    const box = anchor.getBoundingClientRect();
    menu.style.left = `${Math.round(box.left)}px`;
    menu.style.top = `${Math.round(box.bottom + 2)}px`;
    document.body.appendChild(menu);
    const overflow = menu.getBoundingClientRect().bottom - innerHeight;
    if (overflow > 0) menu.style.top = `${Math.max(4, Math.round(box.top - menu.offsetHeight - 2))}px`;
    menu.querySelector('[role="menuitem"]')?.focus();
  };
  document.addEventListener("click", (event) => {
    const currentMenu = document.getElementById(APP_MENU_ID);
    if (currentMenu?.contains(event.target)) return;
    const button = event.target instanceof Element ? event.target.closest("button") : null;
    const topBar = button
      ? [...document.querySelectorAll(".app-header-tint")].find((candidate) =>
        candidate.classList.contains("group/application-menu-top-bar") && candidate.contains(button))
      : null;
    const label = button?.getAttribute("aria-label") || button?.innerText?.trim();
    const menuDefinition = Object.values(applicationMenus).find((candidate) => candidate.labels.includes(label));
    if (topBar && menuDefinition && typeof window.__dreamSkinCommand === "function") {
      event.preventDefault();
      event.stopPropagation();
      event.stopImmediatePropagation();
      if (currentMenu?.dataset.dreamMenu === menuDefinition.ariaLabel) closeApplicationMenu();
      else openApplicationMenu(button, menuDefinition);
      return;
    }
    closeApplicationMenu();
  }, { capture: true, signal: menuAbortController.signal });
  document.addEventListener("keydown", (event) => {
    const menu = document.getElementById(APP_MENU_ID);
    if (!menu) return;
    const items = [...menu.querySelectorAll('[role="menuitem"]')];
    const index = items.indexOf(document.activeElement);
    if (event.key === "Escape") {
      event.preventDefault();
      closeApplicationMenu();
    } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const direction = event.key === "ArrowDown" ? 1 : -1;
      items[(Math.max(index, 0) + direction + items.length) % items.length]?.focus();
    }
  }, { capture: true, signal: menuAbortController.signal });
  const compositionPropertySlot = (slot) => slot === "homeComposer" ? "home-composer" : slot;
  const compositionCssSize = (slot, composition, target) => {
    const metadata = theme?.imageMetadata?.[slot] || (slot === "background" ? theme?.artMetadata : null);
    const sourceWidth = Number(metadata?.width);
    const sourceHeight = Number(metadata?.height);
    const box = typeof target?.getBoundingClientRect === "function" ? target.getBoundingClientRect() : null;
    const targetWidth = Number(slot === "polaroid" ? target?.clientWidth : box?.width);
    const targetHeight = Number(slot === "polaroid" ? target?.clientHeight : box?.height);
    const zoom = Math.max(.5, Math.min(3, Number(composition?.zoom) || 1));
    const fit = composition?.fit === "fill" || composition?.fit === "contain" ? composition.fit : "cover";
    const effectiveZoom = fit === "cover" ? Math.max(1, zoom) : zoom;
    if (!(sourceWidth > 0 && sourceHeight > 0 && targetWidth > 0 && targetHeight > 0)) {
      if (composition?.fit === "fill") return `${(zoom * 100).toFixed(2)}% ${(zoom * 100).toFixed(2)}%`;
      if (composition?.fit === "contain") return zoom === 1 ? "contain" : `${(zoom * 100).toFixed(2)}% auto`;
      if (composition?.fit === "cover") return zoom === 1 ? "cover" : `${(zoom * 100).toFixed(2)}% auto`;
      return null;
    }
    if (fit === "fill") return `${(targetWidth * effectiveZoom).toFixed(2)}px ${(targetHeight * effectiveZoom).toFixed(2)}px`;
    const scale = (fit === "contain"
      ? Math.min(targetWidth / sourceWidth, targetHeight / sourceHeight)
      : Math.max(targetWidth / sourceWidth, targetHeight / sourceHeight)) * effectiveZoom;
    return `${(sourceWidth * scale).toFixed(2)}px ${(sourceHeight * scale).toFixed(2)}px`;
  };
  const clearComponentTokens = (root) => {
    for (const slot of componentSlots) {
      for (const mode of ["light", "dark"]) {
        root?.style.removeProperty(`--dream-component-${slot}-${mode}-rgb`);
        root?.style.removeProperty(`--dream-component-${slot}-${mode}-opacity`);
      }
    }
  };

  const ensureDiffShadowStyles = () => {
    for (const host of document.querySelectorAll("diffs-container")) {
      const shadow = host.shadowRoot;
      if (!shadow) continue;
      let style = shadow.getElementById(DIFF_SHADOW_STYLE_ID);
      if (!style) {
        style = document.createElement("style");
        style.id = DIFF_SHADOW_STYLE_ID;
        shadow.appendChild(style);
      }
      if (style.dataset.dreamVersion !== STYLE_SCHEMA || style.textContent !== diffShadowCss) {
        style.textContent = diffShadowCss;
        style.dataset.dreamVersion = STYLE_SCHEMA;
      }
    }
  };

  const ensure = () => {
    if (window.__CODEX_DREAM_SKIN_DISABLED__) return;
    const root = document.documentElement;
    if (!root) return;
    const shellMain = document.querySelector("main.main-surface") || document.querySelector("main");
    if (!shellMain || !document.body) {
      root.classList.remove("codex-dream-skin");
      delete root.dataset.dreamSidebarBackground;
      delete root.dataset.dreamTransparencyMatch;
      root.style.removeProperty("--dream-art");
      root.style.removeProperty("--dream-sidebar-art");
      root.style.removeProperty("--dream-composer-art");
      root.style.removeProperty("--dream-home-art");
      root.style.removeProperty("--dream-home-composer-art");
      root.style.removeProperty("--dream-polaroid-art");
      root.style.removeProperty("--dream-home-caption");
      root.style.removeProperty("--dream-mode-badge");
      root.style.removeProperty("--dream-profile-badge");
      for (const slot of ["background", "sidebar", "composer", "home", "homeComposer", "polaroid"]) {
        const propertySlot = compositionPropertySlot(slot);
        root.style.removeProperty(`--dream-${propertySlot}-position`);
        root.style.removeProperty(`--dream-${propertySlot}-size`);
        root.style.removeProperty(`--dream-${propertySlot}-zoom`);
      }
      clearComponentTokens(root);
      document.getElementById(STYLE_ID)?.remove();
      document.getElementById(CHROME_ID)?.remove();
      return;
    }
    root.classList.add("codex-dream-skin");
    root.style.setProperty("--dream-art", `url("${artUrl}")`);
    root.style.setProperty("--dream-sidebar-art", `url("${artUrls.sidebar}")`);
    root.style.setProperty("--dream-composer-art", `url("${artUrls.composer}")`);
    root.style.setProperty("--dream-home-art", `url("${artUrls.home}")`);
    root.style.setProperty("--dream-home-composer-art", `url("${artUrls.homeComposer}")`);
    root.style.setProperty("--dream-polaroid-art", `url("${artUrls.polaroid}")`);
    root.style.setProperty("--dream-home-caption", JSON.stringify(decorationProfile.homeCaption));
    root.style.setProperty("--dream-mode-badge", JSON.stringify(decorationProfile.modeBadge));
    root.style.setProperty("--dream-profile-badge", JSON.stringify(decorationProfile.profileBadge));
    const focusX = Number.isFinite(theme?.art?.focusX) ? Math.max(0, Math.min(1, theme.art.focusX)) : .64;
    const focusY = Number.isFinite(theme?.art?.focusY) ? Math.max(0, Math.min(1, theme.art.focusY)) : .44;
    root.style.setProperty("--dream-focus-x", `${(focusX * 100).toFixed(2)}%`);
    root.style.setProperty("--dream-focus-y", `${(focusY * 100).toFixed(2)}%`);
    const compositionFallback = { focusX, focusY, zoom: 1, fit: "auto", offsetX: 0, offsetY: 0 };
    for (const slot of ["background", "sidebar", "composer", "home", "homeComposer", "polaroid"]) {
      const value = theme?.compositions?.[slot] || compositionFallback;
      const slotFocusX = Number.isFinite(value.focusX) ? Math.max(0, Math.min(1, value.focusX)) : focusX;
      const slotFocusY = Number.isFinite(value.focusY) ? Math.max(0, Math.min(1, value.focusY)) : focusY;
      const offsetX = Number.isFinite(value.offsetX) ? Math.max(-1, Math.min(1, value.offsetX)) : 0;
      const offsetY = Number.isFinite(value.offsetY) ? Math.max(-1, Math.min(1, value.offsetY)) : 0;
      const positionX = Math.max(0, Math.min(100, slotFocusX * 100 + offsetX * 25));
      const positionY = Math.max(0, Math.min(100, slotFocusY * 100 + offsetY * 25));
      const propertySlot = compositionPropertySlot(slot);
      root.style.setProperty(`--dream-${propertySlot}-position`, `${positionX.toFixed(2)}% ${positionY.toFixed(2)}%`);
      root.style.setProperty(`--dream-${propertySlot}-zoom`, String(Math.max(.5, Math.min(3, Number(value.zoom) || 1))));
    }
    if (theme?.palette?.accent) root.style.setProperty("--dream-accent", theme.palette.accent);
    const materialTokens = {
      "--dream-custom-light-page": theme?.materials?.light?.page,
      "--dream-custom-light-sidebar": theme?.materials?.light?.sidebar,
      "--dream-custom-light-composer": theme?.materials?.light?.composer,
      "--dream-custom-light-card": theme?.materials?.light?.card,
      "--dream-custom-dark-page": theme?.materials?.dark?.page,
      "--dream-custom-dark-sidebar": theme?.materials?.dark?.sidebar,
      "--dream-custom-dark-composer": theme?.materials?.dark?.composer,
      "--dream-custom-dark-card": theme?.materials?.dark?.card
    };
    for (const [property, value] of Object.entries(materialTokens)) {
      if (Number.isFinite(value) && value >= .04 && value <= .92) root.style.setProperty(property, Number(value).toFixed(3));
      else root.style.removeProperty(property);
    }
    const parseRgb = (value) => {
      const match = /^#([0-9a-f]{6})$/i.exec(String(value || ""));
      if (!match) return null;
      const number = Number.parseInt(match[1], 16);
      return `${(number >> 16) & 255}, ${(number >> 8) & 255}, ${number & 255}`;
    };
    for (const slot of componentSlots) {
      const component = theme?.materials?.components?.[slot];
      for (const mode of ["light", "dark"]) {
        const colorProperty = `--dream-component-${slot}-${mode}-rgb`;
        const opacityProperty = `--dream-component-${slot}-${mode}-opacity`;
        const rgb = parseRgb(component?.[mode]?.color);
        const opacity = component?.[mode]?.opacity;
        if (rgb) root.style.setProperty(colorProperty, rgb);
        else root.style.removeProperty(colorProperty);
        if (Number.isFinite(opacity) && opacity >= .04 && opacity <= .92)
          root.style.setProperty(opacityProperty, Number(opacity).toFixed(3));
        else root.style.removeProperty(opacityProperty);
      }
    }
    root.dataset.dreamAppearance = theme?.appearance || "auto";
    root.dataset.dreamSafeArea = theme?.art?.safeArea || "auto";
    root.dataset.dreamTaskMode = theme?.art?.taskMode || "auto";
    root.dataset.dreamDecoration = decorationProfile.id;
    root.dataset.dreamSidebarBackground = sidebarBackgroundMode;
    root.dataset.dreamTransparencyMatch = matchWorkspaceTransparency ? "on" : "off";

    let style = document.getElementById(STYLE_ID);
    if (!style) {
      style = document.createElement("style");
      style.id = STYLE_ID;
      (document.head || root).appendChild(style);
    }
    if (style.dataset.dreamVersion !== STYLE_SCHEMA) {
      try {
        style.textContent = cssText;
      } catch (error) {
        throw new Error(`Dream skin stylesheet install failed: ${error?.message || error}`);
      }
      style.dataset.dreamVersion = STYLE_SCHEMA;
    }

    ensureDiffShadowStyles();

    const home = document.querySelector('[role="main"]:has([data-testid="home-icon"])');
    const nativeSidebar = document.querySelector("aside.app-shell-left-panel");
    const sidebarNavigation = document.querySelector("nav.sidebar-foreground-muted");
    let structuralSidebar = null;
    if (!nativeSidebar && sidebarNavigation) {
      let candidate = sidebarNavigation.parentElement;
      const referenceHeight = Math.max(
        1,
        Number(window.innerHeight) || Number(document.documentElement?.clientHeight) ||
          Number(shellMain.getBoundingClientRect?.().height) || 1
      );
      while (candidate && candidate !== document.body && candidate !== document.documentElement) {
        const box = typeof candidate.getBoundingClientRect === "function"
          ? candidate.getBoundingClientRect() : null;
        if (box && box.width >= 220 && box.width <= 360 && box.height >= referenceHeight * .65 && box.left < 80) {
          structuralSidebar = candidate;
        }
        candidate = candidate.parentElement;
      }
    }
    const settingsSidebar = nativeSidebar || structuralSidebar;
    const settingsBackLink = typeof settingsSidebar?.querySelector === "function"
      ? settingsSidebar.querySelector('[role="link"]') : null;
    const settingsSurface = typeof shellMain.querySelector === "function"
      ? shellMain.querySelector(".main-surface") : null;
    const settingsSwitches = typeof shellMain.querySelectorAll === "function"
      ? shellMain.querySelectorAll('[role="switch"]') : [];
    const settingsSelects = typeof shellMain.querySelectorAll === "function"
      ? shellMain.querySelectorAll('[role="combobox"]') : [];
    const taskComposer = typeof shellMain.querySelector === "function"
      ? shellMain.querySelector(".composer-surface-chrome") : null;
    const homeArtworkFrame = typeof home?.querySelector === "function"
      ? home.querySelector(":scope > div:first-child > div:first-child > div:first-child") : null;
    const polaroidFrame = home ? document.querySelector(`#${CHROME_ID} .dream-polaroid`) : null;
    const regionTargets = {
      background: sidebarBackgroundMode === "continuous" ? root : shellMain,
      sidebar: settingsSidebar,
      composer: home ? null : taskComposer,
      home: homeArtworkFrame || home,
      homeComposer: home ? taskComposer : null,
      polaroid: polaroidFrame
    };
    for (const slot of ["background", "sidebar", "composer", "home", "homeComposer", "polaroid"]) {
      const value = theme?.compositions?.[slot] || compositionFallback;
      const size = compositionCssSize(slot, value, regionTargets[slot]);
      const propertySlot = compositionPropertySlot(slot);
      if (size) root.style.setProperty(`--dream-${propertySlot}-size`, size);
      else root.style.removeProperty(`--dream-${propertySlot}-size`);
    }
    if (typeof regionResizeObserver !== "undefined" && regionResizeObserver) {
      regionResizeObserver.disconnect();
      for (const target of new Set(Object.values(regionTargets).filter(Boolean))) regionResizeObserver.observe(target);
    }
    const queuedMessageLists = typeof shellMain.querySelectorAll === "function"
      ? [...shellMain.querySelectorAll(
        '.vertical-scroll-fade-mask.hide-scrollbar[class*="max-h-[30dvh]"][class*="gap-px"][class*="px-3"]'
      )]
      : [];
    const queuedMessagePanels = new Set(queuedMessageLists.map((list) => list.parentElement).filter(Boolean));
    for (const list of document.querySelectorAll(".dream-queued-message-list")) {
      list.classList.toggle("dream-queued-message-list", queuedMessageLists.includes(list));
    }
    for (const panel of document.querySelectorAll(".dream-queued-message-panel")) {
      panel.classList.toggle("dream-queued-message-panel", queuedMessagePanels.has(panel));
    }
    for (const list of queuedMessageLists) list.classList.add("dream-queued-message-list");
    for (const panel of queuedMessagePanels) panel.classList.add("dream-queued-message-panel");
    const utilityOpaqueRoot = typeof shellMain.querySelector === "function" ? shellMain.querySelector(
      '.app-shell-main-content-frame [class~="h-full"][class~="min-h-0"][class~="flex-col"][class~="bg-token-main-surface-primary"]'
    ) : null;
    const utilitySearchBand = typeof shellMain.querySelector === "function" ? shellMain.querySelector(
      '.app-shell-main-content-frame [class~="sticky"][class~="bg-token-main-surface-primary"]:has(input)'
    ) : null;
    const inertTaskItems = typeof settingsSidebar?.querySelectorAll === "function"
      ? settingsSidebar.querySelectorAll('[role="listitem"][inert]:has([role="button"].cursor-grab)') : [];
    for (const item of inertTaskItems) {
      unlockedTaskItems.add(item);
      item.inert = false;
      item.removeAttribute("inert");
    }
    // A hover-drawer navigation destroys its portal before the settings route
    // settles, so the settings page cannot require a persistent sidebar/back
    // link. Multiple native settings controls provide a language-independent
    // route signal while the missing composer keeps task pages excluded.
    const sidebarlessSettings = Boolean(
      settingsSurface &&
      (settingsSwitches.length >= 2 || (settingsSwitches.length >= 1 && settingsSelects.length >= 1))
    );
    const settings = Boolean(
      !home && !taskComposer && settingsSurface && (settingsBackLink || sidebarlessSettings)
    );
    const utility = Boolean(!home && !settings && !taskComposer && (utilityOpaqueRoot || utilitySearchBand));
    const task = Boolean(!home && !settings && !utility);
    const sidebarBox = typeof settingsSidebar?.getBoundingClientRect === "function"
      ? settingsSidebar.getBoundingClientRect() : null;
    const mainBox = typeof shellMain.getBoundingClientRect === "function"
      ? shellMain.getBoundingClientRect() : null;
    const sidebarVisible = Boolean(sidebarBox && sidebarBox.width > 1 && sidebarBox.height > 1);
    if (!sidebarVisible) {
      sidebarLifecycle.wasCollapsed = true;
      sidebarLifecycle.revealed = false;
    } else if (sidebarLifecycle.wasCollapsed) {
      sidebarLifecycle.wasCollapsed = false;
      sidebarLifecycle.revealed = true;
    }
    const sidebarMainOverlap = sidebarBox && mainBox
      ? Math.max(0, Math.min(sidebarBox.right, mainBox.right) - Math.max(sidebarBox.left, mainBox.left))
      : 0;
    const sidebarOverlay = Boolean(
      sidebarVisible && (
        Boolean(structuralSidebar && !nativeSidebar) ||
        sidebarLifecycle.revealed ||
        (mainBox && sidebarMainOverlap >= Math.min(64, sidebarBox.width * .25))
      )
    );
    if (!settings && !settingsSidebarRecovery.pending) settingsSidebarRecovery.attempted = false;
    if (settings && settingsSidebar) settingsSidebarRecovery.attempted = false;
    if (settings && !settingsSidebar && !settingsSidebarRecovery.pending &&
        !settingsSidebarRecovery.attempted) {
      settingsSidebarRecovery.attempted = true;
      settingsSidebarRecovery.pending = true;
      const titlebarSidebarToggle = [...document.querySelectorAll("button")].find((button) => {
        if (typeof button.getBoundingClientRect !== "function") return false;
        const box = button.getBoundingClientRect();
        return box.left >= 0 && box.left <= 8 && box.top >= 0 && box.top <= 8 &&
          box.width >= 26 && box.width <= 30 && box.height >= 26 && box.height <= 30;
      });
      if (!titlebarSidebarToggle) {
        settingsSidebarRecovery.pending = false;
      } else {
        titlebarSidebarToggle.click();
        const profileTimer = setTimeout(() => {
          settingsSidebarRecovery.timers.delete(profileTimer);
          const sidebar = document.querySelector("aside.app-shell-left-panel");
          const profileTrigger = sidebar
            ? [...sidebar.querySelectorAll('button[aria-haspopup="menu"]')].find((button) => {
              if (typeof button.getBoundingClientRect !== "function") return false;
              const box = button.getBoundingClientRect();
              return box.height >= 26 && box.top >= Math.max(400, window.innerHeight * .72);
            })
            : null;
          if (!profileTrigger) {
            settingsSidebarRecovery.pending = false;
            return;
          }
          profileTrigger.click();
          const settingsTimer = setTimeout(() => {
            settingsSidebarRecovery.timers.delete(settingsTimer);
            const settingsMenuItem = [...document.querySelectorAll('[role="menu"] [role="menuitem"]')]
              .find((item) => /Ctrl\s*\+,/.test(item.textContent || ""));
            settingsMenuItem?.click();
            settingsSidebarRecovery.pending = false;
          }, 120);
          settingsSidebarRecovery.timers.add(settingsTimer);
        }, 120);
        settingsSidebarRecovery.timers.add(profileTimer);
      }
    }
    for (const candidate of document.querySelectorAll(".dream-sidebar-surface")) {
      if (candidate !== settingsSidebar) candidate.classList.remove("dream-sidebar-surface");
    }
    settingsSidebar?.classList.add("dream-sidebar-surface");
    settingsSidebar?.classList.toggle("dream-settings-sidebar", settings);
    settingsSidebar?.classList.toggle("dream-sidebar-overlay", sidebarOverlay);
    for (const candidate of document.querySelectorAll('[role="main"].dream-home')) {
      if (candidate !== home) candidate.classList.remove("dream-home");
    }
    if (home) home.classList.add("dream-home");

    const suggestionButtons = home ? [...home.querySelectorAll('.group\\/home-suggestions button')] : [];
    suggestionButtons.forEach((button, index) => {
      for (let item = 1; item <= 4; item += 1) button.classList.remove(`dream-suggestion-${item}`);
      if (index < 4) button.classList.add(`dream-suggestion-${index + 1}`);
    });

    shellMain.classList.toggle("dream-home-shell", Boolean(home));
    shellMain.classList.toggle("dream-settings-shell", settings);
    shellMain.classList.toggle("dream-utility-shell", utility);
    shellMain.classList.toggle("dream-task-shell", task);
    const chromeMarkup = decorationProfile.enabled ? `
      <div class="dream-brand"><span class="dream-note">${decorationProfile.note}</span><span><b>${decorationProfile.brand}</b><small>${decorationProfile.subtitle}</small></span></div>
      <div class="dream-signature">${decorationProfile.signature}</div>
      <div class="dream-task-edition"><i></i><span>${decorationProfile.task}</span><small>${decorationProfile.taskEdition}</small></div>
      <div class="dream-sparkles"><i></i><i></i><i></i><i></i><i></i><i></i></div>
      <div class="dream-ribbon"><span>${decorationProfile.ribbonIndex}</span><b>${decorationProfile.ribbon}</b><span>${decorationProfile.ribbonMark}</span></div>
      <div class="dream-polaroid" data-caption="${decorationProfile.polaroid}"></div>` : "";
    let chrome = document.getElementById(CHROME_ID);
    if (!chrome || chrome.parentElement !== document.body) {
      chrome?.remove();
      chrome = document.createElement("div");
      chrome.id = CHROME_ID;
      chrome.setAttribute("aria-hidden", "true");
      document.body.appendChild(chrome);
    }
    if (chrome.dataset.dreamVersion !== STYLE_SCHEMA
        || chrome.dataset.dreamDecoration !== decorationProfile.id) {
      chrome.innerHTML = chromeMarkup;
      chrome.dataset.dreamVersion = STYLE_SCHEMA;
      chrome.dataset.dreamDecoration = decorationProfile.id;
    }
    const shellBox = shellMain.getBoundingClientRect();
    chrome.style.left = `${Math.round(shellBox.left)}px`;
    chrome.style.top = `${Math.round(shellBox.top)}px`;
    chrome.style.width = `${Math.round(shellBox.width)}px`;
    chrome.style.height = `${Math.round(shellBox.height)}px`;
    if (home && homeArtworkFrame) {
      const frameBox = homeArtworkFrame.getBoundingClientRect();
      const ribbonLeft = Math.max(110, Math.min(shellBox.width - 110, frameBox.left - shellBox.left + frameBox.width * .52));
      const ribbonTop = Math.max(72, frameBox.bottom - shellBox.top - 42);
      chrome.style.setProperty("--dream-ribbon-left", `${Math.round(ribbonLeft)}px`);
      chrome.style.setProperty("--dream-ribbon-top", `${Math.round(ribbonTop)}px`);
    } else {
      chrome.style.removeProperty?.("--dream-ribbon-left");
      chrome.style.removeProperty?.("--dream-ribbon-top");
    }
    chrome.classList.toggle("dream-home-shell", Boolean(home));
    chrome.classList.toggle("dream-settings-shell", settings);
    chrome.classList.toggle("dream-utility-shell", utility);
    chrome.classList.toggle("dream-task-shell", task);
  };

  const cleanup = () => {
    window.__CODEX_DREAM_SKIN_DISABLED__ = true;
    document.documentElement?.classList.remove("codex-dream-skin");
    document.documentElement?.style.removeProperty("--dream-art");
    document.documentElement?.style.removeProperty("--dream-sidebar-art");
    document.documentElement?.style.removeProperty("--dream-composer-art");
    document.documentElement?.style.removeProperty("--dream-home-art");
    document.documentElement?.style.removeProperty("--dream-home-composer-art");
    document.documentElement?.style.removeProperty("--dream-polaroid-art");
    document.documentElement?.style.removeProperty("--dream-home-caption");
    document.documentElement?.style.removeProperty("--dream-mode-badge");
    document.documentElement?.style.removeProperty("--dream-profile-badge");
    document.documentElement?.style.removeProperty("--dream-focus-x");
    document.documentElement?.style.removeProperty("--dream-focus-y");
    document.documentElement?.style.removeProperty("--dream-accent");
    for (const property of [
      "--dream-custom-light-page", "--dream-custom-light-sidebar", "--dream-custom-light-composer", "--dream-custom-light-card",
      "--dream-custom-dark-page", "--dream-custom-dark-sidebar", "--dream-custom-dark-composer", "--dream-custom-dark-card"
    ]) document.documentElement?.style.removeProperty(property);
    clearComponentTokens(document.documentElement);
    for (const slot of ["background", "sidebar", "composer", "home", "homeComposer", "polaroid"]) {
      const propertySlot = compositionPropertySlot(slot);
      document.documentElement?.style.removeProperty(`--dream-${propertySlot}-position`);
      document.documentElement?.style.removeProperty(`--dream-${propertySlot}-size`);
      document.documentElement?.style.removeProperty(`--dream-${propertySlot}-zoom`);
    }
    if (document.documentElement) {
      delete document.documentElement.dataset.dreamAppearance;
      delete document.documentElement.dataset.dreamSafeArea;
      delete document.documentElement.dataset.dreamTaskMode;
      delete document.documentElement.dataset.dreamDecoration;
      delete document.documentElement.dataset.dreamSidebarBackground;
      delete document.documentElement.dataset.dreamTransparencyMatch;
    }
    document.querySelectorAll(".dream-home").forEach((node) => node.classList.remove("dream-home"));
    document.querySelectorAll(".dream-home-shell").forEach((node) => node.classList.remove("dream-home-shell"));
    document.querySelectorAll(".dream-settings-shell").forEach((node) => node.classList.remove("dream-settings-shell"));
    document.querySelectorAll(".dream-settings-sidebar").forEach((node) => node.classList.remove("dream-settings-sidebar"));
    document.querySelectorAll(".dream-sidebar-overlay").forEach((node) => node.classList.remove("dream-sidebar-overlay"));
    document.querySelectorAll(".dream-sidebar-surface").forEach((node) => node.classList.remove("dream-sidebar-surface"));
    document.querySelectorAll(".dream-utility-shell").forEach((node) => node.classList.remove("dream-utility-shell"));
    document.querySelectorAll(".dream-task-shell").forEach((node) => node.classList.remove("dream-task-shell"));
    document.querySelectorAll(".dream-queued-message-list").forEach((node) => node.classList.remove("dream-queued-message-list"));
    document.querySelectorAll(".dream-queued-message-panel").forEach((node) => node.classList.remove("dream-queued-message-panel"));
    document.querySelectorAll('[class*="dream-suggestion-"]').forEach((node) => {
      for (let item = 1; item <= 4; item += 1) node.classList.remove(`dream-suggestion-${item}`);
    });
    document.getElementById(STYLE_ID)?.remove();
    document.getElementById(CHROME_ID)?.remove();
    document.getElementById(APP_MENU_ID)?.remove();
    document.querySelectorAll("diffs-container").forEach((host) => host.shadowRoot?.getElementById(DIFF_SHADOW_STYLE_ID)?.remove());
    const state = window[STATE_KEY];
    state?.observer?.disconnect();
    state?.regionResizeObserver?.disconnect();
    if (state?.timer) clearInterval(state.timer);
    if (state?.scheduler?.timeout) clearTimeout(state.scheduler.timeout);
    state?.menuAbortController?.abort();
    for (const pendingTimer of state?.settingsSidebarRecovery?.timers || []) clearTimeout(pendingTimer);
    state?.settingsSidebarRecovery?.timers?.clear?.();
    const urls = state?.artUrls ? Object.values(state.artUrls) : state?.artUrl ? [state.artUrl] : [];
    for (const url of new Set(urls)) URL.revokeObjectURL(url);
    for (const item of unlockedTaskItems) {
      if (item.isConnected && item.classList.contains("pointer-events-none")) item.setAttribute("inert", "");
    }
    unlockedTaskItems.clear();
    delete window[STATE_KEY];
    return true;
  };

  const scheduler = { timeout: null };
  const scheduleEnsure = () => {
    if (scheduler.timeout) clearTimeout(scheduler.timeout);
    scheduler.timeout = setTimeout(() => {
      scheduler.timeout = null;
      ensure();
    }, 180);
  };
  const regionResizeObserver = typeof ResizeObserver === "function" ? new ResizeObserver(scheduleEnsure) : null;
  const observer = new MutationObserver(scheduleEnsure);
  observer.observe(document.documentElement, { childList: true, subtree: true });
  const timer = setInterval(ensure, 5000);
  window[STATE_KEY] = {
    ensure, cleanup, observer, regionResizeObserver, timer, scheduler, menuAbortController,
    artUrl, artUrls, unlockedTaskItems, sidebarLifecycle, settingsSidebarRecovery,
    decorationProfile: decorationProfile.id,
    sidebarBackgroundMode,
    matchWorkspaceTransparency,
    version: "3.10.0"
  };
  try {
    ensure();
  } catch (error) {
    throw new Error(`Dream skin ensure failed: ${error?.message || error}`);
  }
  return { installed: true, version: "3.10.0" };
    })(__DREAM_CSS_JSON__, __DREAM_ART_JSON__, __DREAM_SIDEBAR_ART_JSON__, __DREAM_COMPOSER_ART_JSON__, __DREAM_HOME_ART_JSON__, __DREAM_HOME_COMPOSER_ART_JSON__, __DREAM_POLAROID_ART_JSON__, __DREAM_THEME_JSON__);
  } catch (error) {
    throw new Error(`Dream skin bootstrap failed: ${error?.message || error}\n${error?.stack || ''}`);
  }
})()
