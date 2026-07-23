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
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (message.id !== 1) return;
    if (message.error) reject(new Error(message.error.message));
    else if (message.result.exceptionDetails) reject(new Error(message.result.exceptionDetails.text));
    else resolve(message.result.result.value);
  });
  socket.send(JSON.stringify({
    id: 1,
    method: "Runtime.evaluate",
    params: {
      returnByValue: true,
      expression: `(() => {
        const measure = (node) => {
          if (!node) return null;
          const rect = node.getBoundingClientRect();
          return rect.width > 0 && rect.height > 0
            ? { width: Math.round(rect.width * 100) / 100, height: Math.round(rect.height * 100) / 100,
                ratio: Math.round(rect.width / rect.height * 1000) / 1000 }
            : null;
        };
        const main = document.querySelector('main.main-surface');
        const home = document.querySelector('[role="main"].dream-home');
        const homeHero = home?.querySelector(':scope > div:first-child > div:first-child > div:first-child');
        const ribbon = home ? document.querySelector('#codex-dream-skin-chrome .dream-ribbon') : null;
        const composer = main?.querySelector('.composer-surface-chrome');
        const overlaps = (a, b) => {
          if (!a || !b) return false;
          const first = a.getBoundingClientRect();
          const second = b.getBoundingClientRect();
          return first.left < second.right && first.right > second.left && first.top < second.bottom && first.bottom > second.top;
        };
        return {
          version: window.__CODEX_DREAM_SKIN_STATE__?.version || null,
          viewport: { width: innerWidth, height: innerHeight, ratio: Math.round(innerWidth / innerHeight * 1000) / 1000 },
          route: main?.className || null,
          background: measure(main),
          sidebar: measure(document.querySelector('aside.app-shell-left-panel')),
          composer: measure(composer),
          home: measure(homeHero),
          homeComposer: measure(home ? main?.querySelector('.composer-surface-chrome') : null),
          polaroid: measure(home ? document.querySelector('#codex-dream-skin-chrome .dream-polaroid') : null),
          ribbon: measure(ribbon),
          ribbonComposerOverlap: overlaps(ribbon, composer),
          sizes: Object.fromEntries(['background', 'sidebar', 'composer', 'home', 'home-composer', 'polaroid'].map(slot => [
            slot, document.documentElement.style.getPropertyValue('--dream-' + slot + '-size')
          ]))
        };
      })()`
    }
  }));
});
socket.close();

const parseSize = (value) => {
  const match = /^(\d+(?:\.\d+)?)px (\d+(?:\.\d+)?)px$/.exec(value || "");
  return match ? { width: Number(match[1]), height: Number(match[2]) } : null;
};
const backgroundSize = parseSize(result.sizes?.background);
const sidebarSize = parseSize(result.sizes?.sidebar);
const covers = (size, target) => size && target
  && size.width + .1 >= target.width && size.height + .1 >= target.height;
const pass = result.version === "3.9.4"
  && result.background?.width > 0 && result.background?.height > 0
  && result.sidebar?.width > 0 && result.sidebar?.height > 0
  && (!result.home || (result.ribbon?.width > 0 && result.ribbon?.height > 0 && !result.ribbonComposerOverlap))
  && covers(backgroundSize, result.background)
  && covers(sidebarSize, result.sidebar);
console.log(JSON.stringify({ pass, ...result }, null, 2));
if (!pass) process.exitCode = 1;
