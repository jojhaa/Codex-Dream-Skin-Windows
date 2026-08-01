const port = Number(process.env.CODEX_CDP_PORT || 9335);
const expectedSurface = process.env.CODEX_EXPECTED_SURFACE;
if (!['settings', 'detail', 'review'].includes(expectedSurface)) {
  throw new Error('CODEX_EXPECTED_SURFACE must be settings, detail, or review.');
}

const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => response.json());
const target = targets.find((entry) => entry.type === 'page' && entry.url === 'app://-/index.html');
if (!target?.webSocketDebuggerUrl) throw new Error('Codex page target was not found.');

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener('open', resolve, { once: true });
  socket.addEventListener('error', reject, { once: true });
});

const result = await new Promise((resolve, reject) => {
  const requestId = 1;
  socket.addEventListener('message', (event) => {
    const message = JSON.parse(event.data);
    if (message.id !== requestId) return;
    if (message.error) reject(new Error(message.error.message));
    else if (message.result.exceptionDetails) reject(new Error(message.result.exceptionDetails.text));
    else resolve(message.result.result.value);
  });
  socket.send(JSON.stringify({
    id: requestId,
    method: 'Runtime.evaluate',
    params: {
      returnByValue: true,
      expression: `(() => {
        const surface = ${JSON.stringify(expectedSurface)};
        const main = document.querySelector('main.main-surface, main[data-app-shell-main-surface]');
        const describe = (node) => {
          if (!node) return null;
          const style = getComputedStyle(node);
          return {
            className: String(node.className || ''),
            backgroundColor: style.backgroundColor,
            backgroundImage: style.backgroundImage,
          };
        };
        if (surface === 'settings') {
          const canvas = main?.querySelector('[class~="electron:bg-token-main-surface-primary"]');
          const cards = [...(main?.querySelectorAll('[class~="rounded-2xl"]') || [])]
            .map(describe)
            .filter(Boolean);
          return {
            surface,
            mainClassName: String(main?.className || ''),
            foundations: [describe(canvas)].filter(Boolean),
            readableMaterials: cards,
          };
        }
        if (surface === 'review') {
          const panel = main?.querySelector('aside[data-app-shell-focus-area="right-panel"]');
          const controls = [...(panel?.querySelectorAll('button') || [])].map(describe).filter(Boolean);
          return {
            surface,
            mainClassName: String(main?.className || ''),
            foundations: [...(panel?.querySelectorAll('[class~="bg-token-main-surface-primary"]') || [])]
              .map(describe)
              .filter(Boolean),
            readableMaterials: controls,
          };
        }
        const panel = main?.querySelector('[class~="@container/app-shell-detail-panel"]');
        const section = panel?.parentElement?.matches('section[class~="bg-token-main-surface-primary"]')
          ? panel.parentElement : panel?.closest('section[class~="bg-token-main-surface-primary"]');
        const outer = panel?.closest('[class~="absolute"][class~="bg-token-main-surface-primary"]');
        return {
          surface,
          mainClassName: String(main?.className || ''),
          foundations: [describe(outer), describe(section), describe(panel)].filter(Boolean),
          readableMaterials: [...(panel?.querySelectorAll('[class*="rounded"]') || [])]
            .map(describe)
            .filter(Boolean),
        };
      })()`
    }
  }));
});
socket.close();

const transparent = (entry) => entry.backgroundColor === 'rgba(0, 0, 0, 0)'
  && entry.backgroundImage === 'none';
const pass = result.foundations.length > 0
  && result.foundations.every(transparent)
  && (expectedSurface !== 'settings' || result.mainClassName.includes('dream-settings-shell'))
  && (expectedSurface !== 'settings' || result.readableMaterials.some((entry) => !transparent(entry)))
  && (expectedSurface !== 'review' || result.readableMaterials.some((entry) => !transparent(entry)));

console.log(JSON.stringify({ pass, ...result }, null, 2));
if (!pass) process.exitCode = 1;
