import fs from "node:fs";

const port = Number(process.env.CODEX_CDP_PORT || 9335);
const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then((response) => {
  if (!response.ok) throw new Error(`CDP target list returned HTTP ${response.status}.`);
  return response.json();
});
const target = targets.find((entry) =>
  entry.type === "page" &&
  entry.url === "app://-/index.html" &&
  typeof entry.webSocketDebuggerUrl === "string" &&
  entry.webSocketDebuggerUrl.startsWith(`ws://127.0.0.1:${port}/devtools/page/`));
if (!target) throw new Error("Trusted Codex page target was not found.");

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});

let nextId = 0;
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++nextId;
  const onMessage = (event) => {
    const message = JSON.parse(event.data);
    if (message.id !== id) return;
    socket.removeEventListener("message", onMessage);
    if (message.error) reject(new Error(message.error.message));
    else resolve(message.result);
  };
  socket.addEventListener("message", onMessage);
  socket.send(JSON.stringify({ id, method, params }));
});

try {
  await send("Page.enable");
  const result = await send("Page.captureScreenshot", {
    format: "png",
    fromSurface: true,
    captureBeyondViewport: false,
  });
  const bytes = Buffer.from(result.data || "", "base64");
  const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(pngSignature)) {
    throw new Error("CDP did not return a valid PNG frame.");
  }
  const width = bytes.readUInt32BE(16);
  const height = bytes.readUInt32BE(20);
  if (width < 1 || height < 1) throw new Error("CDP PNG frame had invalid dimensions.");
  const outputPath = process.env.CODEX_CDP_SCREENSHOT_PATH;
  if (outputPath) fs.writeFileSync(outputPath, bytes);
  console.log(JSON.stringify({ pass: true, bytes: bytes.length, width, height, port, outputPath }, null, 2));
} finally {
  socket.close();
}
