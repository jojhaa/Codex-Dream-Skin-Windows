const port = Number(process.env.CODEX_CDP_PORT || 9335);
const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => response.json());
const target = targets.find((entry) => entry.type === "page" && entry.url === "app://-/index.html");
if (!target?.webSocketDebuggerUrl) throw new Error("Codex page target was not found.");

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});

let id = 0;
const pending = new Map();
socket.addEventListener("message", (event) => {
  const message = JSON.parse(event.data);
  if (!message.id || !pending.has(message.id)) return;
  const { resolve, reject } = pending.get(message.id);
  pending.delete(message.id);
  if (message.error) reject(new Error(message.error.message));
  else resolve(message.result);
});

const send = (method, params = {}) => new Promise((resolve, reject) => {
  const requestId = ++id;
  pending.set(requestId, { resolve, reject });
  socket.send(JSON.stringify({ id: requestId, method, params }));
});

const expression = String.raw`(async () => {
  const shell = document.querySelector('main.main-surface, main[data-app-shell-main-surface]') || document.querySelector('main');
  if (!shell || !window.__CODEX_DREAM_SKIN_STATE__?.ensure) {
    throw new Error('Live dream-skin task shell is unavailable.');
  }
  const fixture = document.createElement('section');
  fixture.id = 'dream-live-queued-fixture';
  fixture.style.cssText = 'position:fixed;left:-10000px;top:0;width:420px;z-index:-1';
  fixture.innerHTML = [
    '<div class="vertical-scroll-fade-mask hide-scrollbar flex max-h-[30dvh] flex-col gap-px overflow-x-hidden overflow-y-auto px-3 py-row-y">',
    '  <div class="flex min-h-8 items-center">',
    '    <div class="line-clamp-1 max-h-lh min-w-0 leading-4 text-token-text-secondary" style="height:16px;max-height:16px;overflow:hidden">',
    '      <div class="_markdownContent_liveQueued"><p>待发送信息清晰可见</p></div>',
    '    </div>',
    '    <button type="button">引导</button>',
    '  </div>',
    '</div>',
    '<div class="vertical-scroll-fade-mask hide-scrollbar max-h-[min(70vh,40rem)] flex-col gap-px px-3"></div>'
  ].join('');
  shell.appendChild(fixture);
  window.__CODEX_DREAM_SKIN_STATE__.ensure();
  await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));

  const list = fixture.firstElementChild;
  const activityRail = fixture.lastElementChild;
  const viewport = list.querySelector('.max-h-lh');
  const markdown = list.querySelector('[class*="_markdownContent_"]');
  const text = markdown.querySelector('p');
  const read = (mode) => {
    const markdownStyle = getComputedStyle(markdown);
    const textStyle = getComputedStyle(text);
    const viewportRect = viewport.getBoundingClientRect();
    const textRect = text.getBoundingClientRect();
    const visibleHeight = Math.max(0, Math.min(viewportRect.bottom, textRect.bottom) - Math.max(viewportRect.top, textRect.top));
    return {
      mode,
      paddingTop: markdownStyle.paddingTop,
      paddingBottom: markdownStyle.paddingBottom,
      borderRadius: markdownStyle.borderRadius,
      backgroundColor: markdownStyle.backgroundColor,
      backgroundImage: markdownStyle.backgroundImage,
      lineHeight: markdownStyle.lineHeight,
      textLineHeight: textStyle.lineHeight,
      opacity: textStyle.opacity,
      sinkPx: Number((textRect.top - viewportRect.top).toFixed(2)),
      visibleGlyphRatio: Number((visibleHeight / Math.max(textRect.height, 1)).toFixed(3))
    };
  };
  const wasDark = document.documentElement.classList.contains('electron-dark');
  document.documentElement.classList.remove('electron-dark');
  const light = read('light');
  document.documentElement.classList.add('electron-dark');
  const dark = read('dark');
  document.documentElement.classList.toggle('electron-dark', wasDark);
  const result = {
    version: window.__CODEX_DREAM_SKIN_STATE__.version,
    listMarked: list.classList.contains('dream-queued-message-list'),
    panelMarked: fixture.classList.contains('dream-queued-message-panel'),
    activityRailMarked: activityRail.classList.contains('dream-queued-message-list'),
    light,
    dark
  };
  fixture.remove();
  window.__CODEX_DREAM_SKIN_STATE__.ensure();
  return result;
})()`;

const evaluation = await send("Runtime.evaluate", {
  expression,
  awaitPromise: true,
  returnByValue: true
});
socket.close();
if (evaluation.exceptionDetails) throw new Error(evaluation.exceptionDetails.text);
const result = evaluation.result.value;
const surfacesPass = result.listMarked && result.panelMarked && !result.activityRailMarked;
const layoutPass = [result.light, result.dark].every((mode) =>
  mode.paddingTop === "0px" &&
  mode.paddingBottom === "0px" &&
  mode.borderRadius === "0px" &&
  mode.backgroundImage === "none" &&
  mode.lineHeight === "16px" &&
  mode.textLineHeight === "16px" &&
  mode.opacity === "1" &&
  Math.abs(mode.sinkPx) <= 1 &&
  mode.visibleGlyphRatio >= 0.95
);
console.log(JSON.stringify({ pass: surfacesPass && layoutPass, ...result }, null, 2));
if (!surfacesPass || !layoutPass) process.exitCode = 1;
