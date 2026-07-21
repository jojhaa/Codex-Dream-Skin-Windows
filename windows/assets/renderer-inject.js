(() => {
  try {
    return ((cssText, artDataUrl, theme) => {
  const STATE_KEY = "__CODEX_DREAM_SKIN_STATE__";
  const STYLE_ID = "codex-dream-skin-style";
  const CHROME_ID = "codex-dream-skin-chrome";
  const DIFF_SHADOW_STYLE_ID = "codex-dream-skin-diff-shadow-style";
  const STYLE_SCHEMA = "47";
  const diffShadowCss = `
    :host {
      --vscode-editor-background: transparent !important;
      --vscode-diffEditor-unchangedRegionBackground: transparent !important;
      --vscode-diffEditor-unchangedCodeBackground: transparent !important;
    }

    [data-diffs-header],
    :is([data-diff], [data-file]) {
      --codex-diffs-surface: rgba(250, 253, 252, var(--dream-diff-inner-glass, .08)) !important;
      --codex-diffs-context-surface: rgba(247, 251, 250, .06) !important;
      --codex-diffs-separator-surface: rgba(21, 87, 176, .05) !important;
      --codex-diffs-header-surface: rgba(229, 243, 246, .08) !important;
      --codex-diffs-context-number: rgba(60, 88, 112, .10) !important;
      --codex-diffs-addition-number: rgba(25, 123, 99, .14) !important;
      --codex-diffs-deletion-number: rgba(197, 39, 70, .13) !important;
      --codex-diffs-addition-hover: rgba(25, 123, 99, .16) !important;
      --codex-diffs-deletion-hover: rgba(197, 39, 70, .15) !important;
      --diffs-bg: rgba(250, 253, 252, var(--dream-diff-inner-glass, .08)) !important;
      --diffs-bg-buffer: rgba(236, 246, 247, .08) !important;
      --diffs-bg-separator: rgba(21, 87, 176, .03) !important;
      --diffs-bg-context-override: rgba(247, 251, 250, .06) !important;
      --diffs-bg-separator-override: rgba(21, 87, 176, .05) !important;
      background-color: rgba(250, 253, 252, var(--dream-diff-inner-glass, .08)) !important;
    }

    :is([data-diff], [data-file]) :is([data-code], [data-gutter], [data-content]) {
      background-color: transparent;
    }

    :host-context(html.electron-dark) [data-diffs-header],
    :host-context(html.electron-dark) :is([data-diff], [data-file]) {
      --codex-diffs-surface: rgba(7, 27, 46, var(--dream-diff-inner-glass, .16)) !important;
      --codex-diffs-context-surface: rgba(7, 27, 46, .14) !important;
      --codex-diffs-separator-surface: rgba(103, 185, 255, .08) !important;
      --codex-diffs-header-surface: rgba(15, 56, 89, .16) !important;
      --codex-diffs-context-number: rgba(183, 204, 224, .14) !important;
      --codex-diffs-addition-number: rgba(103, 219, 186, .18) !important;
      --codex-diffs-deletion-number: rgba(255, 124, 154, .17) !important;
      --codex-diffs-addition-hover: rgba(103, 219, 186, .20) !important;
      --codex-diffs-deletion-hover: rgba(255, 124, 154, .19) !important;
      --diffs-bg: rgba(7, 27, 46, var(--dream-diff-inner-glass, .16)) !important;
      --diffs-bg-buffer: rgba(10, 39, 64, .16) !important;
      --diffs-bg-separator: rgba(103, 185, 255, .07) !important;
      --diffs-bg-context-override: rgba(7, 27, 46, .14) !important;
      --diffs-bg-separator-override: rgba(103, 185, 255, .08) !important;
      background-color: rgba(7, 27, 46, var(--dream-diff-inner-glass, .16)) !important;
      color: var(--dream-ink, #e9f5ff) !important;
    }
  `;
  window.__CODEX_DREAM_SKIN_DISABLED__ = false;

  const previous = window[STATE_KEY];
  if (previous?.observer) previous.observer.disconnect();
  if (previous?.timer) clearInterval(previous.timer);
  if (previous?.scheduler?.timeout) clearTimeout(previous.scheduler.timeout);
  const unlockedTaskItems = previous?.unlockedTaskItems || new Set();
  const artUrl = previous?.artUrl || (() => {
    const comma = artDataUrl.indexOf(",");
    const binary = atob(artDataUrl.slice(comma + 1));
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
    const mime = artDataUrl.slice(5, comma).split(";")[0] || "application/octet-stream";
    return URL.createObjectURL(new Blob([bytes], { type: mime }));
  })();
  const existingStyle = document.getElementById(STYLE_ID);
  if (existingStyle) {
    try {
      existingStyle.textContent = cssText;
    } catch (error) {
      throw new Error(`Dream skin stylesheet update failed: ${error?.message || error}`);
    }
    existingStyle.dataset.dreamVersion = STYLE_SCHEMA;
  }

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
      root.style.removeProperty("--dream-art");
      document.getElementById(STYLE_ID)?.remove();
      document.getElementById(CHROME_ID)?.remove();
      return;
    }
    root.classList.add("codex-dream-skin");
    root.style.setProperty("--dream-art", `url("${artUrl}")`);
    const focusX = Number.isFinite(theme?.art?.focusX) ? Math.max(0, Math.min(1, theme.art.focusX)) : .64;
    const focusY = Number.isFinite(theme?.art?.focusY) ? Math.max(0, Math.min(1, theme.art.focusY)) : .44;
    root.style.setProperty("--dream-focus-x", `${(focusX * 100).toFixed(2)}%`);
    root.style.setProperty("--dream-focus-y", `${(focusY * 100).toFixed(2)}%`);
    if (theme?.palette?.accent) root.style.setProperty("--dream-accent", theme.palette.accent);
    root.dataset.dreamAppearance = theme?.appearance || "auto";
    root.dataset.dreamSafeArea = theme?.art?.safeArea || "auto";
    root.dataset.dreamTaskMode = theme?.art?.taskMode || "auto";

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
    const settingsSidebar = document.querySelector("aside.app-shell-left-panel");
    const settingsBackLink = typeof settingsSidebar?.querySelector === "function"
      ? settingsSidebar.querySelector('[role="link"]') : null;
    const settingsSurface = typeof shellMain.querySelector === "function"
      ? shellMain.querySelector(".main-surface") : null;
    const taskComposer = typeof shellMain.querySelector === "function"
      ? shellMain.querySelector(".composer-surface-chrome") : null;
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
    const settings = Boolean(!home && settingsBackLink && settingsSurface && !taskComposer);
    const utility = Boolean(!home && !settings && !taskComposer && (utilityOpaqueRoot || utilitySearchBand));
    const task = Boolean(!home && !settings && !utility);
    settingsSidebar?.classList.toggle("dream-settings-sidebar", settings);
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
    const chromeMarkup = `
      <div class="dream-brand"><span class="dream-note">01</span><span><b>桥本环奈 · 蓝色瞬间</b><small>KANNA HASHIMOTO / CODEX EDITION</small></span></div>
      <div class="dream-signature">Kanna / 環奈</div>
      <div class="dream-task-edition"><i></i><span>03 / KANNA BLUE</span><small>TASK EDITION</small></div>
      <div class="dream-sparkles"><i></i><i></i><i></i><i></i><i></i><i></i></div>
      <div class="dream-ribbon"><span>01</span><b>BLUE MOMENT</b><span>●</span></div>
      <div class="dream-polaroid"></div>`;
    let chrome = document.getElementById(CHROME_ID);
    if (!chrome || chrome.parentElement !== document.body) {
      chrome?.remove();
      chrome = document.createElement("div");
      chrome.id = CHROME_ID;
      chrome.setAttribute("aria-hidden", "true");
      document.body.appendChild(chrome);
    }
    if (chrome.dataset.dreamVersion !== STYLE_SCHEMA) {
      chrome.innerHTML = chromeMarkup;
      chrome.dataset.dreamVersion = STYLE_SCHEMA;
    }
    const shellBox = shellMain.getBoundingClientRect();
    chrome.style.left = `${Math.round(shellBox.left)}px`;
    chrome.style.top = `${Math.round(shellBox.top)}px`;
    chrome.style.width = `${Math.round(shellBox.width)}px`;
    chrome.style.height = `${Math.round(shellBox.height)}px`;
    chrome.classList.toggle("dream-home-shell", Boolean(home));
    chrome.classList.toggle("dream-settings-shell", settings);
    chrome.classList.toggle("dream-utility-shell", utility);
    chrome.classList.toggle("dream-task-shell", task);
  };

  const cleanup = () => {
    window.__CODEX_DREAM_SKIN_DISABLED__ = true;
    document.documentElement?.classList.remove("codex-dream-skin");
    document.documentElement?.style.removeProperty("--dream-art");
    document.documentElement?.style.removeProperty("--dream-focus-x");
    document.documentElement?.style.removeProperty("--dream-focus-y");
    document.documentElement?.style.removeProperty("--dream-accent");
    if (document.documentElement) {
      delete document.documentElement.dataset.dreamAppearance;
      delete document.documentElement.dataset.dreamSafeArea;
      delete document.documentElement.dataset.dreamTaskMode;
    }
    document.querySelectorAll(".dream-home").forEach((node) => node.classList.remove("dream-home"));
    document.querySelectorAll(".dream-home-shell").forEach((node) => node.classList.remove("dream-home-shell"));
    document.querySelectorAll(".dream-settings-shell").forEach((node) => node.classList.remove("dream-settings-shell"));
    document.querySelectorAll(".dream-settings-sidebar").forEach((node) => node.classList.remove("dream-settings-sidebar"));
    document.querySelectorAll(".dream-utility-shell").forEach((node) => node.classList.remove("dream-utility-shell"));
    document.querySelectorAll(".dream-task-shell").forEach((node) => node.classList.remove("dream-task-shell"));
    document.querySelectorAll(".dream-queued-message-list").forEach((node) => node.classList.remove("dream-queued-message-list"));
    document.querySelectorAll(".dream-queued-message-panel").forEach((node) => node.classList.remove("dream-queued-message-panel"));
    document.querySelectorAll('[class*="dream-suggestion-"]').forEach((node) => {
      for (let item = 1; item <= 4; item += 1) node.classList.remove(`dream-suggestion-${item}`);
    });
    document.getElementById(STYLE_ID)?.remove();
    document.getElementById(CHROME_ID)?.remove();
    document.querySelectorAll("diffs-container").forEach((host) => host.shadowRoot?.getElementById(DIFF_SHADOW_STYLE_ID)?.remove());
    const state = window[STATE_KEY];
    state?.observer?.disconnect();
    if (state?.timer) clearInterval(state.timer);
    if (state?.scheduler?.timeout) clearTimeout(state.scheduler.timeout);
    if (state?.artUrl) URL.revokeObjectURL(state.artUrl);
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
  const observer = new MutationObserver(scheduleEnsure);
  observer.observe(document.documentElement, { childList: true, subtree: true });
  const timer = setInterval(ensure, 5000);
  window[STATE_KEY] = { ensure, cleanup, observer, timer, scheduler, artUrl, unlockedTaskItems, version: "3.4.9" };
  try {
    ensure();
  } catch (error) {
    throw new Error(`Dream skin ensure failed: ${error?.message || error}`);
  }
  return { installed: true, version: "3.4.9" };
    })(__DREAM_CSS_JSON__, __DREAM_ART_JSON__, __DREAM_THEME_JSON__);
  } catch (error) {
    throw new Error(`Dream skin bootstrap failed: ${error?.message || error}\n${error?.stack || ''}`);
  }
})()
