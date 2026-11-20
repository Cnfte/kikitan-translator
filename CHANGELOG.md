# 更新日志 (Changelog)

本项目基于 [YusufOzmen01/kikitan-translator](https://github.com/YusufOzmen01/kikitan-translator)（上游基线提交 `768eb89`，原版著作权归属 Copyright 2024 SergioMarquina，遵循 MIT 许可证）进行二次开发与工程重构。所有版本记录如下。

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
