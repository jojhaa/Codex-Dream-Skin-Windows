# Codex Dream Skin

<p align="center">
  <strong>中文</strong> · <a href="./README.en.md">English</a>
</p>

<p align="center">
  <strong>jojhaa 个人维护版 · v0.3.2</strong><br>
  Windows 原生主题管理器 · 分区域构图 · 实时热重载 · 浅色/深色玻璃材质
</p>

<p align="center">
  <strong>给 Codex 桌面端换一张会呼吸的脸。</strong><br>
  外部主题 / 换肤工具 · 本机 CDP 注入 · 不改官方安装包
</p>

<p align="center">
  一张图，一种心情 · 写代码，也要有氛围感
</p>

<p align="center">
  非 OpenAI 官方产品。不修改 <code>.app</code> / <code>app.asar</code> / WindowsApps。
</p>

## v0.3.2 个人维护版

这是一个发布在 [`jojhaa/Codex-Dream-Skin-Windows`](https://github.com/jojhaa/Codex-Dream-Skin-Windows) 下的独立个人仓库，不会向 `Fei-Away/Codex-Dream-Skin` 推送。当前版本以 Windows 为主要开发和验收平台，在原有外部主题方案上加入可视化管理器和完整的 Kanna Blue 玻璃主题。

核心能力：

- **Windows 原生管理器**：WinUI 3 工作区，支持主题库、导入导出、历史恢复、诊断和安全接管。
- **六区域独立图片与构图**：主背景、侧边栏、任务输入框、首页照片框、首页输入框和 Polaroid 分别设置图片、焦点、缩放、填充与偏移。
- **所见即所得编辑**：按 Codex 实际区域比例取景，不生成破坏原图的裁剪副本；支持真实尺寸刷新和推荐构图恢复。
- **实时主题预览**：浅色/深色即时切换、自动配色、肤色规避、对比度警告、组件级颜色与透明度编辑。
- **热重载与普通启动接管**：保存前可连续预览；可选后台接管普通 Codex 启动，并动态适配更新后的 Store 版本。
- **完整界面玻璃化**：任务页、设置页、插件/站点/拉取请求、聊天、终端、审查侧栏、菜单、悬停抽屉和消息组件均有浅色/深色适配。
- **安全边界明确**：仅使用本机回环 CDP，不修改 Codex 安装目录，不替换官方二进制，不创建隐藏托盘快捷方式。

详细更新内容见 [`CHANGELOG.md`](./CHANGELOG.md)。

### Windows 快速启动

脚本模式：

```powershell
git clone https://github.com/jojhaa/Codex-Dream-Skin-Windows.git
cd Codex-Dream-Skin-Windows\windows
powershell -ExecutionPolicy RemoteSigned -File .\scripts\install-dream-skin.ps1
powershell -ExecutionPolicy RemoteSigned -File .\scripts\start-dream-skin.ps1
```

原生主题管理器开发启动（需要 .NET 10 SDK）：

```powershell
dotnet run --project .\app\CodexDreamSkin\CodexDreamSkin.csproj -c Release -p:Platform=x64
```

## 赞助商

<p align="center">
  <a href="https://passion8.cc/register?aff=TuPe">
    <img src="docs/images/sponsor-passion8.png" alt="Passion8" height="72">
  </a>
</p>

<p align="center">
  <strong>更智能的连接 · 更热爱的创造</strong><br>
  <sub>热爱驱动 · 无限可能 · Connect AI · Power Creation</sub>
</p>

<p align="center">
  感谢 <a href="https://passion8.cc/register?aff=TuPe"><strong>passion8.cc</strong></a> 赞助本项目。<br>
  满血 AI 中转：官方模型直连，无降智、无套壳；一行配置接入 Codex / Claude Code / Grok。
</p>

<p align="center">
  <sub>
    换肤与 API 配置互相独立，本项目不会自动改写你的模型供应商设置。
  </sub>
</p>

## 效果预览

一张图，一种心情。下面都是可落地的主题示意效果：

<p align="center">
  <img src="docs/images/gallery/skin-01.jpg" alt="粉系定制" width="900"><br>
  <sub>粉系定制</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-02.jpg" alt="财神打工" width="900"><br>
  <sub>财神打工版</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-03.jpg" alt="红白科幻" width="900"><br>
  <sub>红白科幻</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-04.jpg" alt="清透定制" width="900"><br>
  <sub>清透定制</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-05.jpg" alt="灵感小宇宙" width="900"><br>
  <sub>灵感小宇宙</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-06.jpg" alt="紫夜限定" width="900"><br>
  <sub>紫夜限定</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-07.jpg" alt="初音未来" width="900"><br>
  <sub>初音未来</sub>
</p>

<p align="center">
  <img src="docs/images/gallery/skin-08.jpg" alt="舞台黑金" width="900"><br>
  <sub>舞台黑金</sub>
</p>

## 它能做什么

- **真·可交互**：侧栏、建议卡、项目选择、输入框都是原生控件，不是整窗假截图贴上去
- **可换图**：换一张喜欢的图，就能变成你的主题
- **可恢复**：一键还原官方外观
- **相对安全**：本机回环 CDP 注入，不改官方二进制与签名

## 快速开始

仓库内按平台放了现成脚本（实现细节不同，效果都是「主题化 Codex」）：

| 平台 | 目录 | 入口 |
|------|------|------|
| Apple Silicon / Intel Mac | [`macos/`](./macos/) | 双击 `Install Codex Dream Skin.command` |
| Windows | [`windows/`](./windows/) | `scripts/install-dream-skin.ps1` → `start-dream-skin.ps1` |

更细的说明：

- Mac：[`macos/README.md`](./macos/README.md)
- Windows：[`windows/SKILL.md`](./windows/SKILL.md)
- 路径对照：[`docs/platforms.md`](./docs/platforms.md)
- 项目记录：[`docs/PROJECT.md`](./docs/PROJECT.md)

## 反馈与贡献

- **Issue：** 请用 [Issue 模板](./.github/ISSUE_TEMPLATE/)（Bug / 功能）；已关闭空白 Issue。提交前建议先跑 Verify / Restore 自检。
- **PR：** 请按 [PR 模板](./.github/pull_request_template.md) 写清改动，并勾选对应自测（如 `macos/tests/run-tests.sh`、verify / restore）。

## 安全边界

- CDP 只绑 `127.0.0.1`，主题运行期间勿跑来路不明的本机程序
- 不修改官方安装目录与代码签名
- **不会**自动改写 API Key / Base URL；中转与换肤分开

## 许可与声明

- 见 [`macos/LICENSE`](./macos/LICENSE)（MIT）与 [`macos/NOTICE.md`](./macos/NOTICE.md)
- 非 OpenAI 官方产品；Codex 及相关权利归其权利人
- 效果图中的人物 / IP 形象仅作主题示意；商用或公开再分发请自行确认肖像权与商标授权

---

Star 一下，然后挑一张图，把你的 Codex 变成今天想要的样子。
