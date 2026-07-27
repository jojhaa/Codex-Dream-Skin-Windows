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
        const main = document.querySelector('main.main-surface');
        const sidebar = document.querySelector('aside.app-shell-left-panel');
        const composer = main?.querySelector('.composer-surface-chrome');
        const read = (node, pseudo = null) => {
          if (!node) return null;
          const style = getComputedStyle(node, pseudo);
          return {
            backgroundImage: style.backgroundImage,
            backgroundRepeat: style.backgroundRepeat,
            backgroundSize: style.backgroundSize,
            backgroundPosition: style.backgroundPosition
          };
        };
        return {
          version: window.__CODEX_DREAM_SKIN_STATE__?.version || null,
          route: main?.className || null,
          dark: document.documentElement.classList.contains('electron-dark'),
          sidebar: read(sidebar),
          main: read(main),
          composer: read(composer),
          composerBefore: read(composer, '::before'),
          composerAfter: read(composer, '::after')
        };
      })()`
    }
  }));
});
socket.close();

const blobCount = (value) => (value?.match(/blob:/g) || []).length;
const onlyNoRepeat = (value) => (value || "").split(",").every((part) => part.trim() === "no-repeat");
const isTask = /dream-task-shell/.test(result.route || "");
const expectedComposerSize = process.env.EXPECT_COMPOSER_SIZE;
const composerSizeMatches = expectedComposerSize
  ? result.composerAfter?.backgroundSize === expectedComposerSize
  : /^\d+(?:\.\d+)?px \d+(?:\.\d+)?px$/.test(result.composerAfter?.backgroundSize || "");
const pass = result.version === "3.10.0" && (!isTask || (
  blobCount(result.sidebar?.backgroundImage) === 1
  && onlyNoRepeat(result.sidebar?.backgroundRepeat)
  && blobCount(result.main?.backgroundImage) === 1
  && onlyNoRepeat(result.main?.backgroundRepeat)
  && blobCount(result.composer?.backgroundImage) === 0
  && blobCount(result.composerAfter?.backgroundImage) === 1
  && result.composerAfter?.backgroundRepeat === "no-repeat"
  && composerSizeMatches
));
console.log(JSON.stringify({ pass, ...result }, null, 2));
if (!pass) process.exitCode = 1;
