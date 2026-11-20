# Kikitan Translator (Liquid Glass Mod)

<div align="center">
<a href="https://sergiomarquina.booth.pm/items/6073050">
<img src="https://media.buyee.jp/guide/addtobuyee/assets/img/store-logo-booth.png" alt="Booth Store" width="150" align="center">
</a>
<a href="https://buymeacoffee.com/sergiomarquina">
<img src="https://i.imgur.com/l7NBjqk.png" alt="Buy Me a Coffee" width="150" height="45" align="center">
</a>
<br><br>
<img width=500 src="https://i.imgur.com/C9fSR9O.png" />
</div>

> **著作权与上游声明 / Copyright & Attribution**
>
> 本项目为基于开源项目 [YusufOzmen01/kikitan-translator](https://github.com/YusufOzmen01/kikitan-translator)（上游提交基线 `768eb89`）的二次修改版（v2.2.3-mod）。  
> 原项目著作权归 **SergioMarquina**（`Copyright 2024 SergioMarquina`）及原仓库贡献者所有，完整授权条款保留于根目录 [LICENSE.md](LICENSE.md)（MIT License）。请优先通过上方原版作者主页支持原作者。

---

## 项目简介

Kikitan Translator 是一款面向 VRChat 的实时语音识别、翻译与聊天框（OSC）同步工具，支持中文、英语（多地区口音）、日语、韩语、西班牙语、法语、德语、俄语等十余种主要语言的实时互译，亦提供纯语音转文字（Transcription Only）模式及 SteamVR / 桌面悬浮字幕叠加显示。

---

## 本改版改动说明（相较于上游原版）

详细按时间线排列的修复与技术条目请参阅 [CHANGELOG.md](CHANGELOG.md)。相较于原版，本改版主要包含以下核心重构与功能新增：

### 1. 无边框液态玻璃界面与 Windows 桌面稳定性修复
- **全主题 Liquid Glass 视觉重构**：重写前端主舞台、设置面板与交互控件，支持深色/浅色双主题切换及 **0–100 玻璃磨砂透度实时调节**（`glass_opacity`），支持 640px 至高分屏自适应布局。
- **彻底无边框化与 Win32 DWM 兼容修复**：
  - 使用 Photino `Chromeless` + `WS_POPUP` 模式配合 Windows 11 DWM Acrylic 背景属性，消除原生标题栏按钮重影及顶部 `29px` 原生深色横条。
  - 剥离 `WS_EX_LAYERED` 与开启 `WS_CLIPCHILDREN`，修复窗口向右拉伸超过初始 `900px` 宽度后右侧无响应、竖直分界线以及调整大小时背景闪烁的问题。
  - 修正无边框窗口最大化工作区边界计算，使最大化后严格贴合屏幕可用工作区且不遮挡系统任务栏；支持四边及四角 8 向鼠标拖拽缩放。
  - 修复前端 `window_state` 消息解析异常与窗口关闭/重连状态竞态，避免程序启动或退出时产生异常崩溃（如 `0xc0000005` 及内存映射管道冲突）。
- **独立 UI 诊断日志**：新增 `%APPDATA%\Kikitan Translator\logs\ui_debug.log`，实时记录 Win32 窗口样式、DWM 边界与前端 DOM 布局遥测数据。

### 2. AI 语音识别后置语义修正（AI Semantic Correction）
- 新增识别纠错管线：语音识别生成原始文本后，可先交由指定大模型修正同音错字、语气词结巴与断句偏差，再送入翻译引擎或聊天框发送。
- 支持选择 **自动跟随当前 AI 服务商** 或 **单独指定内置/自定义服务商**，并允许自定义系统提示词（Prompt）。

### 3. 多模型翻译服务商、自定义端点与低延迟策略
在原有 Google Translate、Groq、Gemini 基础上，扩展完整大模型接入体系（**Settings -> Translation API**）：

| 服务商 | 说明 |
| --- | --- |
| Google Translate | 免费直连，无需 API 密钥 |
| Groq | OpenAI 兼容对话接口，支持免费额度 |
| Gemini | 与 Gemini 实时语音识别协同工作的翻译模型 |
| OpenAI | OpenAI 官方及兼容格式对话翻译 |
| Claude | Anthropic Messages API（`x-api-key` 鉴权） |
| DeepSeek | 深度求索 OpenAI 兼容接口 |
| Kimi | 月之暗面 Moonshot 对话接口 |
| GLM | 智谱 BigModel 开放平台接口 |

- **自动模型拉取与自定义 Base URL**：填写 API Key 后自动请求对应接口拉取可用对话模型列表（过滤非文本对话模型），每个内置服务商均支持单独覆盖 Base URL 以适配中转站或镜像网关。
- **任意自定义中转服务商**：支持在 **Custom providers** 中添加自定义服务商，选择 `OpenAI` / `Claude` / `Gemini` 协议格式，或使用 **Detect protocol** 自动检测端点协议类型。
- **默认关闭深度思考模式（`disable_thinking`）**：针对实时聊天低延迟需求，默认按服务商规范下发关闭推理参数（如 DeepSeek/Kimi/GLM/Claude 使用 `thinking: {"type": "disabled"}`，OpenAI/Groq 使用 `reasoning_effort: "none"`）；遇到强制开启推理的模型或拒绝该参数的接口时会自动降级兼容并记忆，亦可手动关闭此开关。

### 4. 内置网络代理管理与 OSC 双路分发
- **多代理管理与连通性测试（Settings -> Proxy）**：支持添加 HTTP、HTTPS、SOCKS5、SOCKS4/4a 代理及账密认证，单选启用并可实时测试延迟；所有翻译请求、语音识别、模型拉取与 AI 纠错请求均统一经由所选代理转发。
- **OSC 双端口同步输出**：支持配置目标 IP（`osc_address`）及第二 OSC 目标端口（`osc_secondary_port`），可将打字状态与翻译消息同时分发至 VRChat 及其他监听程序。

### 5. 桌面字幕遮罩修正与专用安装向导
- **桌面字幕条带化与 CJK 自动换行**：桌面叠加窗口改为仅覆盖屏幕底部工作区区域并附带防激活属性，不遮挡系统任务栏、不抢夺焦点、不进入 Alt+Tab；新增中文/日文/韩文无空格文本自动换行排版；支持启动时自动清理异常残留遮罩进程并容错打开命名管道。
- **Windows 安装与卸载向导（`KikitanTranslator.Installer`）**：新增独立安装程序，集成 WebView2 Runtime / .NET 9 Desktop Runtime / Windows DWM / OSC 端口状态检查、自定义安装路径、配置保留与控制面板卸载支持。
- **安全隔离上游自动更新**：置空 `Constants.UPDATE_FEED_URL`，避免联网自动更新检查将当前改版客户端覆写回上游原版二进制文件。

---

## 配置文件与日志位置

- **配置文件**：`%APPDATA%\Kikitan Translator\config.json`（Linux 下位于用户应用数据目录内的 `config.json`）
- **运行日志**：`%APPDATA%\Kikitan Translator\logs\`
  - 主程序与后端服务日志：`YYYY-MM-DDTHH-MM-SS.log`
  - 窗口与前端布局调试日志：`ui_debug.log`

---

## 构建说明（.NET 9 + React / Vite）

本仓库已从早期的 Rust/Tauri 架构全面迁移至 **.NET 9 + Photino.NET + React/Vite** 方案。

### 环境依赖
- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)（仓库 `global.json` 指定基线为 `9.0.100`）
- [Node.js](https://nodejs.org/)（建议 v20 及以上，用于构建 `KikitanTranslator.Photino/UserInterface` 前端资源）
- Windows 平台运行环境：Microsoft Edge WebView2 Runtime（Windows 11 默认内置）

### 常用构建命令

```powershell
# 1. 克隆仓库
git clone https://github.com/Cnfte/kikitan-translator.git
cd kikitan-translator

# 2. 安装前端依赖
cd KikitanTranslator.Photino/UserInterface
npm install
cd ../..

# 3. 还原并构建 Windows Release 版本（将自动触发前端 npm run build 及 Overlay 构建）
dotnet restore kikitan-translator.sln
dotnet build KikitanTranslator.Photino/KikitanTranslator.Photino.csproj -c Release -r win-x64

# 4. 生成单文件图形安装向导程序（如需构建安装器）
dotnet publish KikitanTranslator.Installer/KikitanTranslator.Installer.csproj -c Release -r win-x64
```

*注：开发调试 UI 时，可在 `KikitanTranslator.Photino/UserInterface` 目录运行 `npm run dev`，并使用 Debug 配置启动主程序加载本地 Vite 开发服务器（`http://localhost:1420`）。*

---

## 开源许可

本项目遵循上游原有 MIT 开源许可证条款，详细文本见 [LICENSE.md](LICENSE.md)（`Copyright 2024 SergioMarquina`）。
