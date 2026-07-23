const port = Number(process.env.CODEX_CDP_PORT || 9335);
const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => response.json());
const target = targets.find((entry) => entry.type === "page" && entry.url === "app://-/index.html");
if (!target?.webSocketDebuggerUrl) throw new Error("Codex page target was not found.");

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});

let nextId = 0;
const pending = new Map();
socket.addEventListener("message", (event) => {
  const message = JSON.parse(event.data);
  const request = pending.get(message.id);
  if (!request) return;
  pending.delete(message.id);
  if (message.error) request.reject(new Error(message.error.message));
  else if (message.result?.exceptionDetails) request.reject(new Error(message.result.exceptionDetails.text));
  else request.resolve(message.result?.result?.value);
});
const evaluate = (expression) => new Promise((resolve, reject) => {
  const id = ++nextId;
  pending.set(id, { resolve, reject });
  socket.send(JSON.stringify({
    id,
    method: "Runtime.evaluate",
    params: { expression, returnByValue: true, awaitPromise: true }
  }));
});

const result = await evaluate(`(async () => {
  const expected = { 文件: 6, 编辑: 8, 视图: 18, 帮助: 8 };
  const output = {};
  for (const [label, count] of Object.entries(expected)) {
    const topBar = [...document.querySelectorAll('.app-header-tint')]
      .find((candidate) => candidate.classList.contains('group/application-menu-top-bar'));
    const button = [...(topBar?.querySelectorAll('button') || [])]
      .find((candidate) => (candidate.getAttribute('aria-label') || candidate.innerText.trim()) === label);
    button?.click();
    await new Promise((resolve) => setTimeout(resolve, 40));
    const menu = document.getElementById('codex-dream-skin-app-menu');
    const box = menu?.getBoundingClientRect();
    output[label] = {
      count: menu?.querySelectorAll('[role="menuitem"]').length || 0,
      expected: count,
      ariaLabel: menu?.getAttribute('aria-label') || null,
      height: box?.height || 0,
      overflow: box ? Math.max(0, box.bottom - innerHeight) : -1
    };
  }
  document.getElementById('codex-dream-skin-app-menu')?.remove();
  return {
    version: window.__CODEX_DREAM_SKIN_STATE__?.version || null,
    binding: typeof window.__dreamSkinCommand,
    menus: output
  };
})()`);
const nativeCommand = process.env.INVOKE_NATIVE_COMMAND;
if (nativeCommand) {
  await evaluate(`window.__dreamSkinCommand(${JSON.stringify(JSON.stringify({ command: nativeCommand }))})`);
}

socket.close();
const pass = result.version === "3.9.4"
  && result.binding === "function"
  && Object.values(result.menus).every((menu) =>
    menu.count === menu.expected && menu.ariaLabel?.endsWith("菜单") && menu.overflow === 0);
console.log(JSON.stringify({ pass, ...result, invokedNativeCommand: nativeCommand || null }, null, 2));
if (!pass) process.exitCode = 1;
