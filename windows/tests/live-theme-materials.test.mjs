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
        return {
          version: window.__CODEX_DREAM_SKIN_STATE__?.version || null,
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
  "--dream-custom-dark-composer": process.env.EXPECT_DARK_COMPOSER
};
const checks = Object.entries(expected)
  .filter(([, value]) => value !== undefined)
  .map(([name, value]) => ({ name, expected: Number(value).toFixed(3), actual: result.tokens[name] }));
const expectedBackgroundPosition = process.env.EXPECT_BACKGROUND_POSITION || '64.00% 44.00%';
const pass = result.version === "3.9.4"
  && Object.values(result.tokens).every((value) => /^(?:0|1|0?\.\d{3})$/.test(value))
  && Object.entries(result.components).every(([name, value]) =>
    name.endsWith('-rgb') ? /^\d{1,3}, \d{1,3}, \d{1,3}$/.test(value) : /^(?:0|1|0?\.\d{3})$/.test(value))
  && ['background', 'sidebar', 'composer', 'home', 'home-composer', 'polaroid'].every((slot) =>
    result.compositions[`--dream-${slot}-position`] === (slot === 'background' ? expectedBackgroundPosition : '64.00% 44.00%')
    && result.compositions[`--dream-${slot}-zoom`] === '1')
  && checks.every((check) => check.actual === check.expected);
console.log(JSON.stringify({ pass, ...result, checks }, null, 2));
if (!pass) process.exitCode = 1;
