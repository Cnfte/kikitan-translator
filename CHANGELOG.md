# 更新日志 (Changelog)

本项目基于 [YusufOzmen01/kikitan-translator](https://github.com/YusufOzmen01/kikitan-translator)（上游基线提交 `768eb89`，原版著作权归属 Copyright 2024 SergioMarquina，遵循 MIT 许可证）进行二次开发与工程重构。所有版本记录如下。

---

## v2.3.0 (2026-11-28)

### 1. PC 单框聚合双语悬浮窗与自动滚动
- **单框时间线聚合**：将「对方」（桌面音频翻译）与「我」（麦克风与手动输入）的实时发言按时间顺序聚合在同一个无边框大圆角磨砂卡片中显示，每条按统一层级展示（对方：译文大字在上、原文小字在下；我：原文大字在上、译文小字在下）。
- **自适应尺寸与平滑自动滚动**：短句自动收缩为紧凑气泡，多轮对话超出高度上限后自动平滑滚动至最新一条，顶部带平滑 0% 透明渐隐遮罩与右侧极简滚动条指示。
- **实时控制与停止即隐藏**：支持 6 方位屏幕锚点定位（底部居中/左下/右下/顶部居中/左上/右上）、75%–150% 字号缩放、一键实时预览测试与清空字幕；点击停止监听后立即清空并隐藏悬浮窗。
- **高分屏矢量排版与环形管道**：启用 `PerMonitorV2` 高分屏适配与按字体/颜色合批绘制，修复中英混排字距；底层升级为 8 槽环形内存映射管道，避免双人同时说话互相覆盖。

### 2. VRChat 实用功能增强
- **快捷短语一键发送**：主界面输入栏左侧新增快捷短语抽屉，内置高频 VRChat 社交短语，点击即翻译并同步发送至 VRChat Chatbox 与桌面悬浮窗，支持自定义增删与持久化。
- **自定义翻译词典**：支持在设置中配置专有名词、好友昵称与 VRChat 常用术语固定映射，自动作用于 AI 翻译提示词与 Google 翻译引擎。
- **VRChat 144 字符上限智能保护**：自动适配 VRChat `/chatbox/input` 长度上限，超长句优先保留完整译文并平滑精简括号内原文，避免消息被截断或静默丢弃。
- **历史记录升级**：对话历史持久化至本地存储（最多保留 100 条），支持实时搜索过滤、单条复制、一键重发至 VRChat、导出 `.txt` 双语记录与清空。

### 3. 全新 WPF 交互式安装包与云端自动更新
- **全新 WPF 安装向导**：使用 WPF 重构自包含单文件安装程序（无需预先安装 .NET 运行库即可启动），自动检测并支持交互式一键安装缺失依赖（WebView2 Runtime、.NET 9.0 Windows Desktop Runtime、ASP.NET Core 9.0 Runtime、Visual C++ Redistributable x64）。
- **云端自动检测更新与日志弹窗**：启动时自动拉取云端 `gx.txt` 核对版本号，发现新版本后自动从指定地址下载并触发静默无缝覆盖更新（自动保留 `config.json`）；更新完成首次启动时自动拉取并展示云端 `CHANGELOG.md`，关闭后当前版本不再重复弹出。

---

## v2.2.3-mod (2026-11-20)

最新提交基线：`4786107`

### 1. 界面重构与 Windows 窗口框架修复
- **Apple Liquid Glass 界面体系与响应式布局**
  - 重构主界面、设置页、API 管理页、代理管理页与快速入门弹窗样式（`globals.css`、`page.tsx`、`Kikitan.tsx`、`Settings.tsx`）。
  - 支持深色/浅色主题及 `glass_opacity`（0–100）实时玻璃磨砂透度调节。
  - 优化 640px 至 1920px+ 宽度自适应，窄窗口下自动隐藏次要版本标签与分隔线并通过文本截断避免顶栏重叠溢出；补全 `cn` / `en` / `jp` / `kr` 四语种本地化键值。
- **无边框窗口与 Windows 11 DWM 合成修复**
  - 在 `Program.cs` 中启用 `.SetChromeless(true)`、`SetUseOsDefaultLocation(false)` 与 `SetUseOsDefaultSize(false)`，从窗口创建阶段使用 `WS_POPUP`。
  - 通过 `AttachBorderlessFrameSubclass` 将主窗口样式规范为 `WS_POPUP | WS_CLIPCHILDREN | WS_MINIMIZEBOX | WS_MAXIMIZEBOX`（`0x96030000`），剥离 `WS_CAPTION | WS_SYSMENU | WS_THICKFRAME` 并保留 `WS_EX_APPWINDOW`（`0x00040000`），彻底消除 Windows 11 原生标题栏三键叠加及顶部 `29px` 深色标题栏条。
  - 剥离 Photino 透明窗口默认附加的 `WS_EX_LAYERED` 扩展样式，解决窗口横向拉伸超过初始 `900px` 后右侧区域无法点击及出现垂直分界线的问题。
  - 拦截 `WM_ERASEBKGND` 并启用 `WS_CLIPCHILDREN`，消除拖拽缩放过程中的 GDI 背景闪烁。
  - 在 `WM_GETMINMAXINFO` 与 `WM_NCCALCSIZE` 中通过 `TryGetMonitorInfo` 按当前显示器工作区钳制尺寸与坐标，修复最大化时产生的边缘偏移与四周留缝问题。
- **前端通信与调试诊断工具**
  - 修复 `photino.ts` 在处理纯字符串 `window_state` 消息时直接调用 `JSON.parse` 触发的 `SyntaxError` 异常。
  - 在 `Program.cs`、`MessageHandler.cs`、`Connector.cs` 与 `UiDebugLog.cs` 中实现 Win32 / WebView2 联合几何遥测日志，运行时写入 `%APPDATA%\Kikitan Translator\logs\ui_debug.log`。

### 2. AI 语义修正与多服务商翻译引擎
- **语音识别后置 AI 语义修正（`AiSemanticCorrector.cs`）**
  - 在语音识别完成并进入翻译前增加语义纠错环节，自动修正同音错字、口吃重复及标点断句偏差。
  - 支持 `ai_semantic_correction_provider`（默认 `auto` 自动沿用当前 AI 翻译服务商，或单独指定内置/自定义服务商）与 `ai_semantic_correction_prompt` 自定义提示词。
- **多服务商原生接入（`ApiProviders.cs`、`HttpTranslatorBase.cs`）**
  - 新增 OpenAI、Claude（Anthropic Messages API）、DeepSeek、Kimi（Moonshot）及 GLM（智谱 BigModel）内置翻译服务商，与 Groq 共用统一 HTTP 重试与超时控制基类。
  - 全面支持自定义 Base URL 与基于 `FetchModels.cs` 的自动可用模型列表拉取（自动过滤 embedding / audio / image 等非对话模型）。
- **自定义中转服务商与低延迟推理抑制**
  - 新增 `CustomProviderConfig`，支持用户添加任意数量的兼容端点（`openai` / `claude` / `gemini` 协议），并在前端 `ApiSettings.tsx` 提供 **协议自动探测（Detect protocol）** 功能。
  - 新增 `disable_thinking` 开关（默认开启）：针对 DeepSeek、Kimi、GLM、Claude 下发关闭思维链参数，针对 OpenAI、Groq 下发 `reasoning_effort: "none"`；当遇到强制推理模型或不支持该参数的接口时自动移除该字段并缓存记录，保障翻译低延迟与可用性。

### 3. 网络代理、OSC 分发、桌面遮罩与安装向导
- **内置多代理隧道（`ProxyTunnel.cs`、`NetworkClientFactory.cs`、`ProxySettings.tsx`）**
  - 支持配置多组 HTTP、HTTPS、SOCKS5 及 SOCKS4/4a 代理（支持用户名/密码验证）与单选切换。
  - 新增 `TestProxy` 连通性与延迟测试；所有翻译、语音识别、模型列表及语义修正 HTTP 请求均统一通过当前激活代理路由。
- **VRChat OSC 双端口同步分发（`Chatbox.cs`）**
  - 新增 `osc_address` 自定义目标主机、`osc_secondary_port` 副端口及 `osc_enable_dual_dispatch` 双路分发选项，支持同时向 VRChat 与第三方辅助工具同步发送输入状态与消息包。
- **桌面遮罩与稳定性改进（`KikitanTranslator.Overlay`、`Manager.cs`）**
  - 优化桌面字幕遮罩（`Form1.cs`）：窗口收缩至屏幕工作区底部条带以避免遮挡任务栏及被录屏软件识别为全屏窗口，补充 `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` 防止抢夺焦点或出现在 Alt+Tab 列表中；调整 `Overlay/Program.cs` 守护轮询间隔以降低空闲 CPU 占用。
  - 新增 SkiaSharp `WrapText` 中文/日文/韩文等无空格语种字符级分词换行支持。
  - 启动前自动清理异常退出残留的 `KikitanTranslator.Overlay` 进程，并将 `OverlayWriter.cs` 的内存映射文件从 `CreateNew` 改为 `CreateOrOpen`；为 `Connector.cs` 增加窗口关闭周期状态守卫，防止关闭/初始化竞态导致崩溃。
  - 将 `Constants.UPDATE_FEED_URL` 置空以禁用上游自动更新覆盖。
- **Windows 安装与卸载向导（`KikitanTranslator.Installer`）**
  - 新增桌面端安装向导（`Installer-win64.exe`），支持环境自检（WebView2 Runtime 在线安装引导、.NET 9 桌面运行时检测、Windows DWM 合成状态检测、OSC 端口状态）、自定义安装路径、配置 `config.json` 迁移保留、快捷方式创建与标准控制面板卸载注册。

---

## 相关提交历史（自上游基线 `768eb89` 起）
- `4786107` — `fix(win32): use chromeless WS_POPUP style to eliminate 29px DWM titlebar band`
- `038c02d` — `fix(win32): strip native caption buttons and clamp maximized work area bounds`
- `04e9739` — `fix: strip WS_EX_LAYERED to prevent 900px DWM hit-test cutoff and split line on rightward resize`
- `2db96af` — `fix: prevent resize JSON parse errors, WS_CLIPCHILDREN/WM_ERASEBKGND flicker, and header overflow`
- `f62238c` — `feat: add live Win32 and WebUI resize debug telemetry logger (ui_debug.log)`
- `82c488c` — `fix: responsive narrow/wide UI alignment and flush borderless PostMessage resize`
- `b2d3d31` — `feat: Liquid Glass UI redesign, borderless resizable window, crash fix, and interactive installer`
