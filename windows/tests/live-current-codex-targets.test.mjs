import assert from "node:assert/strict";

const port = Number(process.env.CODEX_CDP_PORT || 9335);
const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => response.json());
const pages = targets.filter((entry) => entry.type === "page" && entry.url.startsWith("app://"));
const mainTarget = pages.find((entry) => entry.url === "app://-/index.html");
const auxiliaryTarget = pages.find((entry) => entry.url.includes("initialRoute="));
assert.ok(mainTarget?.webSocketDebuggerUrl, "Current Codex main app target was not found.");
assert.ok(auxiliaryTarget?.webSocketDebuggerUrl, "Current Codex auxiliary app target was not found.");

async function evaluate(target, expression) {
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });
  try {
    return await new Promise((resolve, reject) => {
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
        params: { returnByValue: true, expression },
      }));
    });
  } finally {
    socket.close();
  }
}

const main = await evaluate(mainTarget, `(() => ({
  stableShell: !!document.querySelector('main[data-app-shell-main-surface]'),
  stableHeader: !!document.querySelector('header[data-app-shell-application-menu-bar]'),
  installed: document.documentElement.classList.contains('codex-dream-skin'),
  styleSchema: document.getElementById('codex-dream-skin-style')?.dataset.dreamVersion || null,
  routeClass: document.querySelector('main[data-app-shell-main-surface]')?.className || ''
}))()`);
assert.equal(main.stableShell, true, "Latest Codex must expose its stable main-surface marker.");
assert.equal(main.stableHeader, true, "Latest Codex must expose its stable application-header marker.");
assert.equal(main.installed, true, "The theme must install on the current Codex main target.");
assert.equal(main.styleSchema, "70", "The current compatibility stylesheet must be active.");
assert.match(main.routeClass, /dream-(?:home|task|settings|utility)-shell/,
  "The current Codex main surface must receive a theme route class.");

const auxiliary = await evaluate(auxiliaryTarget, `(() => ({
  stableShell: !!document.querySelector('main[data-app-shell-main-surface]'),
  installed: document.documentElement.classList.contains('codex-dream-skin'),
  stylePresent: !!document.getElementById('codex-dream-skin-style')
}))()`);
assert.equal(auxiliary.stableShell, false, "The auxiliary target must not impersonate the Codex main surface.");
assert.equal(auxiliary.installed, false, "The auxiliary app target must remain unthemed.");
assert.equal(auxiliary.stylePresent, false, "The auxiliary app target must not receive theme CSS.");

console.log("PASS: current Codex main target is themed and auxiliary app targets remain isolated.");
