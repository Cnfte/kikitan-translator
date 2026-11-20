import * as React from "react";

import {
    Select,
    MenuItem,
    Button,
    IconButton,
    Tooltip,
    CircularProgress,
    Snackbar,
    Alert,
    Slide,
} from "@mui/material";

import {
    PlayArrow as PlayArrowIcon,
    Stop as StopIcon,
    History as HistoryIcon,
    Close as CloseIcon,
    MicNone as MicIcon,
    Translate as TranslateIcon,
    SwapHoriz as SwapHorizIcon,
    AutoAwesome as AutoAwesomeIcon,
    Headphones as HeadphonesIcon,
    ArrowUpward as SendIcon,
    ContentCopy as ContentCopyIcon,
    Check as CheckIcon,
    GraphicEq as GraphicEqIcon,
} from "@mui/icons-material";

import {
    app_state,
    langSource,
    langTo,
} from "../util/constants";

import { localization } from "../util/localization";
import { providerLabel } from "../util/providers";
import {
    controlKikitan,
    manualTranslate,
    registerMicrophoneChangedCallback,
    registerNotificationCallback,
    registerRecognitionCallback,
    setConfig
} from "../util/photino.ts";

export default function Kikitan({ state }: { state: app_state }) {
    const [messageHistory, setMessageHistory] = React.useState<{ source: string, translation: string, timestamp: number }[]>([]);
    const [detecting, setDetecting] = React.useState(false);
    const [detection, setDetection] = React.useState<string>("");
    const [translation, setTranslation] = React.useState<string>("");
    const [showMessageHistory, setShowMessageHistory] = React.useState(false);
    const [textInputValue, setTextInputValue] = React.useState("");
    const [copied, setCopied] = React.useState(false);

    const [notification, setNotification] = React.useState<{
        open: boolean;
        message: string;
        severity: "success" | "error" | "warning" | "info";
    }>({ open: false, message: "", severity: "info" });

    const showNotification = (message: string, severity: "success" | "error" | "warning" | "info" = "info") => {
        switch (message) {
            case "GROQ_NO_API_KEY":
                setNotification({ open: true, message: localization.no_api_key_configured_for_groq[state.config.language], severity });
                break;
            case "GROQ_INVALID_API_KEY":
                setNotification({ open: true, message: localization.invalid_api_key_groq[state.config.language], severity });
                break;
            case "GEMINI_NO_API_KEY":
                setNotification({ open: true, message: localization.no_api_key_configured_for_gemini[state.config.language], severity });
                break;
            case "GEMINI_INVALID_API_KEY":
                setNotification({ open: true, message: localization.invalid_api_key_gemini[state.config.language], severity });
                break;
            default: {
                const apiKeyError = /^([A-Z0-9_]+)_(NO_API_KEY|INVALID_API_KEY)$/.exec(message);
                if (apiKeyError != null) {
                    const text = apiKeyError[2] == "NO_API_KEY" ? localization.no_api_key_for : localization.invalid_api_key_for;
                    setNotification({
                        open: true,
                        message: text[state.config.language].replace("{provider}", providerLabel(apiKeyError[1])),
                        severity
                    });
                } else {
                    setNotification({ open: true, message, severity });
                }
                break;
            }
        }
    };

    React.useEffect(() => {
        // @ts-ignore
        registerRecognitionCallback((r: string, t: string, f: boolean) => {
            if (r.length != 0) setDetection(r);
            if (t.length != 0) setTranslation(t);
            setDetecting(!f);

            if (f) {
                setMessageHistory(prev => {
                    let copy = [...prev];
                    if (copy.length >= 50) copy = copy.slice(0, 49);
                    copy.unshift({
                        source: r,
                        translation: t,
                        timestamp: Date.now()
                    });
                    sessionStorage.setItem("history", JSON.stringify(copy));
                    return copy;
                });
            }
        });

        registerMicrophoneChangedCallback(() => showNotification(localization.microphone_updated[state.config.language], "warning"));
        registerNotificationCallback((msg: string, level: number) => showNotification(msg, level == 1 ? "warning" : level == 2 ? "error" : level == 3 ? "success" : "info"));
        controlKikitan(true);

        if (sessionStorage.getItem("history") != null) {
            try {
                setMessageHistory(JSON.parse(sessionStorage.getItem("history")!));
            } catch (e) {
                console.error(e);
            }
        }
    }, []);

    const formatTimestamp = (timestamp: number) => {
        const date = new Date(timestamp);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    };

    const handleSendManual = () => {
        if (!textInputValue.trim()) return;
        manualTranslate(textInputValue.trim());
        setTextInputValue("");
    };

    const handleCopy = (text: string) => {
        if (!text) return;
        navigator.clipboard.writeText(text);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    const isRunning = state.status === 2;
    const isBusy = state.status === 1;

    const cleanSelectSx = {
        height: 32,
        minWidth: 118,
        fontWeight: 600,
        fontSize: "0.84rem",
        color: "var(--text-primary)",
        bgcolor: "rgba(var(--glass-clear-rgb), 0.08)",
        borderRadius: "9999px",
        px: 0.5,
        "& .MuiOutlinedInput-notchedOutline": { border: "1px solid var(--border-glass)" },
        "&:hover .MuiOutlinedInput-notchedOutline": { border: "1px solid var(--border-glass-strong)" },
        "&.Mui-focused .MuiOutlinedInput-notchedOutline": { border: "1px solid var(--accent-blue)" },
    };

    return (
        <div className="flex flex-col flex-1 h-full min-h-0 w-full max-w-full gap-3 justify-between select-none font-sans overflow-hidden">
            
            {/* 1. Spacious Apple Translate Dual Stage (No nested inner boxes) */}
            <div className="grid grid-cols-[minmax(0,1fr),auto,minmax(0,1fr)] gap-3 items-stretch flex-1 min-h-0 min-w-0">
                
                {/* Left Stage: Source Speech Recognition */}
                <section className="liquid-glass-regular rounded-3xl px-6 py-5 flex flex-col justify-between min-h-0 flex-1 min-w-0">
                    
                    {/* Top Row: Source Language & AI Semantic Correction Pill */}
                    <div className="flex items-center justify-between gap-2 shrink-0">
                        <div className="flex items-center gap-2">
                            <span className="w-7 h-7 rounded-full liquid-glass-clear flex items-center justify-center apple-text-secondary">
                                <MicIcon sx={{ fontSize: 16 }} />
                            </span>
                            <Select
                                size="small"
                                sx={cleanSelectSx}
                                value={state.config.source_language}
                                onChange={(e) => setConfig("source_language", e.target.value)}
                            >
                                {langSource.map((element) => (
                                    <MenuItem key={element.code} value={element.code}>
                                        {element.name[state.config.language]}
                                    </MenuItem>
                                ))}
                            </Select>
                        </div>

                        <Tooltip title={localization.ai_semantic_correction_desc?.[state.config.language] ?? "语音识别后由AI修正同音错字再翻译"}>
                            <button
                                onClick={() => setConfig("ai_semantic_correction", !state.config.ai_semantic_correction)}
                                className={`flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium transition-all ${
                                    state.config.ai_semantic_correction
                                        ? "badge-pastel-blue font-semibold"
                                        : "liquid-glass-clear apple-text-secondary hover:apple-text-primary"
                                }`}
                            >
                                <AutoAwesomeIcon sx={{ fontSize: 13 }} />
                                <span>{localization.ai_semantic_correction?.[state.config.language] ?? "AI 语义修正"}</span>
                            </button>
                        </Tooltip>
                    </div>

                    {/* Center Canvas: Generous Typography without Box-inside-Box */}
                    <div className="flex-1 min-h-0 flex flex-col justify-center py-4 overflow-y-auto">
                        {detection ? (
                            <p className={`text-xl md:text-2xl font-medium tracking-tight leading-relaxed apple-text-primary select-text break-words ${detecting ? "opacity-75" : ""}`}>
                                {detection}
                            </p>
                        ) : (
                            <div className="flex flex-col items-center justify-center text-center gap-2 my-auto">
                                <div className={`w-11 h-11 rounded-full liquid-glass-clear flex items-center justify-center ${isRunning ? "text-[var(--accent-blue)]" : "apple-text-tertiary"}`}>
                                    <GraphicEqIcon sx={{ fontSize: 22 }} />
                                </div>
                                <p className="text-sm font-medium apple-text-secondary">
                                    {isRunning ? "正在监听语音输入..." : "当前处于待机状态"}
                                </p>
                                <p className="text-xs apple-text-tertiary">
                                    {localization.recognized_text?.[state.config.language] ?? "语音识别原文将实时显示于此"}
                                </p>
                            </div>
                        )}
                    </div>

                    {/* Subtle Caption Footer */}
                    <div className="flex items-center justify-between text-[11px] apple-text-tertiary pt-2 border-t apple-divider shrink-0">
                        <span>{localization.recognized_text?.[state.config.language] ?? "语音识别 (原文)"}</span>
                        {state.config.ai_semantic_correction && (
                            <span className="font-mono text-[10px] text-[var(--accent-blue)] font-medium">
                                STT / AI CORRECTION / TRANSLATE
                            </span>
                        )}
                    </div>
                </section>

                {/* Center Language Swap Trigger */}
                <div className="flex flex-col items-center justify-center">
                    <Tooltip title={localization.swap_languages?.[state.config.language] ?? "互换语言"}>
                        <button
                            onClick={() => {
                                const prevSource = state.config.source_language;
                                setConfig("source_language", state.config.target_language);
                                setConfig("target_language", prevSource);
                            }}
                            className="w-10 h-10 rounded-full liquid-glass-regular hover:bg-[var(--accent-blue)] hover:!text-white apple-text-secondary flex items-center justify-center transition-all shadow-sm"
                        >
                            <SwapHorizIcon sx={{ fontSize: 19 }} />
                        </button>
                    </Tooltip>
                </div>

                {/* Right Stage: Target Translation Output */}
                <section className="liquid-glass-regular rounded-3xl px-6 py-5 flex flex-col justify-between min-h-0 flex-1 min-w-0">
                    
                    {/* Top Row: Target Language & Copy Action */}
                    <div className="flex items-center justify-between gap-2 shrink-0">
                        <div className="flex items-center gap-2">
                            <span className="w-7 h-7 rounded-full liquid-glass-clear flex items-center justify-center apple-text-secondary">
                                <TranslateIcon sx={{ fontSize: 15 }} />
                            </span>
                            <Select
                                size="small"
                                sx={cleanSelectSx}
                                value={state.config.target_language}
                                onChange={(e) => setConfig("target_language", e.target.value)}
                            >
                                {langTo.map((element) => (
                                    <MenuItem key={element.code} value={element.code}>
                                        {element.name[state.config.language]}
                                    </MenuItem>
                                ))}
                            </Select>
                        </div>

                        <button
                            disabled={!translation}
                            onClick={() => handleCopy(translation)}
                            className="liquid-glass-clear flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium apple-text-secondary hover:apple-text-primary disabled:opacity-35 transition-all"
                        >
                            {copied ? <CheckIcon sx={{ fontSize: 13, color: "var(--accent-green)" }} /> : <ContentCopyIcon sx={{ fontSize: 13 }} />}
                            <span>{copied ? "已复制" : "复制"}</span>
                        </button>
                    </div>

                    {/* Center Canvas: Translation Output */}
                    <div className="flex-1 min-h-0 flex flex-col justify-center py-4 overflow-y-auto">
                        {state.config.speech_to_text_only ? (
                            <div className="flex flex-col items-center justify-center text-center gap-2 my-auto max-w-sm mx-auto">
                                <p className="text-sm font-medium apple-text-secondary">
                                    {localization.vr_mode_stt_only[state.config.language] ?? "仅语音转文字模式"}
                                </p>
                                <p className="text-xs apple-text-tertiary leading-relaxed">
                                    {localization.translation_disabled[state.config.language]}
                                </p>
                            </div>
                        ) : translation ? (
                            <p className="text-xl md:text-2xl font-semibold tracking-tight leading-relaxed apple-text-primary select-text break-words">
                                {translation}
                            </p>
                        ) : (
                            <div className="flex flex-col items-center justify-center text-center gap-2 my-auto">
                                <div className="w-11 h-11 rounded-full liquid-glass-clear flex items-center justify-center apple-text-tertiary">
                                    <TranslateIcon sx={{ fontSize: 20 }} />
                                </div>
                                <p className="text-sm font-medium apple-text-secondary">
                                    等待译文输出
                                </p>
                                <p className="text-xs apple-text-tertiary">
                                    译文将自动通过 OSC 同步至 VRChat 聊天框
                                </p>
                            </div>
                        )}
                    </div>

                    {/* Subtle Caption Footer */}
                    <div className="flex items-center justify-between text-[11px] apple-text-tertiary pt-2 border-t apple-divider shrink-0">
                        <span>{localization.translated_text?.[state.config.language] ?? "翻译结果 (发送至 VRChat)"}</span>
                        <span className="font-mono text-[10px]">
                            {state.config.send_to_chatbox ? "OSC CHATBOX SYNCED" : "OSC CHATBOX PAUSED"}
                        </span>
                    </div>
                </section>
            </div>

            {/* 2. Unified Apple Floating Control Dock */}
            <div className="liquid-glass-regular rounded-2xl px-3.5 py-2 flex items-center gap-2.5 shrink-0">
                
                {/* Master Start / Stop Capsule Button */}
                <button
                    disabled={isBusy}
                    onClick={() => controlKikitan(state.status == 0)}
                    className={`h-9 px-4 rounded-full font-semibold text-xs flex items-center justify-center gap-1.5 transition-all shrink-0 ${
                        isRunning
                            ? "bg-[var(--pill-active-bg)] text-[var(--pill-active-fg)] shadow-sm"
                            : "bg-[var(--accent-blue)] text-white shadow-sm"
                    } disabled:opacity-50`}
                >
                    {isBusy ? (
                        <CircularProgress size={14} sx={{ color: "currentColor" }} />
                    ) : isRunning ? (
                        <>
                            <StopIcon sx={{ fontSize: 15 }} />
                            <span>{localization.stop_listening?.[state.config.language] ?? "停止监听"}</span>
                        </>
                    ) : (
                        <>
                            <PlayArrowIcon sx={{ fontSize: 16 }} />
                            <span>{localization.start_listening?.[state.config.language] ?? "开始监听"}</span>
                        </>
                    )}
                </button>

                {/* Compact Microphone Selector */}
                <Select
                    size="small"
                    sx={{
                        height: 36,
                        minWidth: 145,
                        maxWidth: 210,
                        fontSize: "0.76rem",
                        fontWeight: 500,
                        color: "var(--text-primary)",
                        bgcolor: "rgba(var(--glass-clear-rgb), 0.08)",
                        borderRadius: "9999px",
                        "& .MuiOutlinedInput-notchedOutline": { border: "1px solid var(--border-glass)" },
                    }}
                    value={state.config.microphone || ""}
                    onChange={(e) => setConfig("microphone", e.target.value)}
                >
                    {state.microphones.map((mic) => (
                        <MenuItem key={mic.name} value={mic.name} sx={{ fontSize: "0.78rem" }}>
                            {(mic.name.includes("(") && mic.name.includes(")"))
                                ? mic.name.match(/\(([^)]+)\)/)?.[1]
                                : mic.name}
                            {mic.default ? ` (${localization.default[state.config.language]})` : ""}
                        </MenuItem>
                    ))}
                </Select>

                {/* Desktop Audio Capture Pill */}
                <Tooltip title={localization.enable_desktop_capture[state.config.language]}>
                    <button
                        disabled={state.is_linux}
                        onClick={() => setConfig("desktop_translation", !state.config.desktop_translation)}
                        className={`h-9 px-3 rounded-full flex items-center gap-1.5 text-xs font-medium shrink-0 transition-all ${
                            state.config.desktop_translation
                                ? "badge-pastel-blue font-semibold"
                                : "liquid-glass-clear apple-text-secondary hover:apple-text-primary"
                        }`}
                    >
                        <HeadphonesIcon sx={{ fontSize: 14 }} />
                        <span className="hidden md:inline">{localization.desktop_audio?.[state.config.language] ?? "桌面音频"}</span>
                    </button>
                </Tooltip>

                {/* Integrated Apple iMessage-Style Input Capsule */}
                <div className="flex-1 flex items-center h-9 rounded-full liquid-glass-well pl-4 pr-1 gap-2 min-w-0">
                    <input
                        type="text"
                        placeholder={localization.type_message_placeholder?.[state.config.language] ?? "输入内容并按 Enter 发送至 VRChat..."}
                        value={textInputValue}
                        onChange={(e) => setTextInputValue(e.target.value)}
                        onKeyDown={(e) => {
                            if (e.key === "Enter") {
                                handleSendManual();
                            }
                        }}
                        className="flex-1 bg-transparent border-none outline-none text-xs apple-text-primary placeholder:apple-text-tertiary min-w-0"
                    />
                    <button
                        onClick={handleSendManual}
                        disabled={!textInputValue.trim()}
                        className="w-7 h-7 rounded-full bg-[var(--accent-blue)] text-white flex items-center justify-center disabled:opacity-30 transition-all shrink-0"
                        title={localization.send[state.config.language]}
                    >
                        <SendIcon sx={{ fontSize: 15 }} />
                    </button>
                </div>

                {/* History Sheet Trigger */}
                <Tooltip title={localization.message_history[state.config.language]}>
                    <button
                        onClick={() => setShowMessageHistory(true)}
                        className="liquid-glass-clear h-9 px-3 rounded-full text-xs font-medium apple-text-secondary hover:apple-text-primary flex items-center gap-1.5 shrink-0 transition-all"
                    >
                        <HistoryIcon sx={{ fontSize: 16 }} />
                        <span className="font-mono text-[11px] font-semibold">
                            {messageHistory.length}
                        </span>
                    </button>
                </Tooltip>
            </div>

            {/* 3. Message History Sheet Modal */}
            {showMessageHistory && (
                <div className="fixed inset-0 z-50 flex items-center justify-center backdrop-blur-md bg-black/40 p-4">
                    <div className="flex flex-col w-full max-w-[720px] h-[76vh] liquid-glass-sheet rounded-3xl p-6 overflow-hidden">
                        
                        <div className="flex items-center justify-between pb-3.5 border-b apple-divider">
                            <div className="flex items-center gap-2">
                                <HistoryIcon sx={{ fontSize: 19 }} className="apple-text-primary" />
                                <h2 className="text-base font-semibold apple-text-primary tracking-tight">
                                    {localization.message_history[state.config.language]}
                                </h2>
                                <span className="font-mono text-xs apple-text-tertiary">
                                    ({messageHistory.length})
                                </span>
                            </div>
                            <IconButton
                                onClick={() => setShowMessageHistory(false)}
                                size="small"
                                sx={{ color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "9999px", p: 0.7 }}
                            >
                                <CloseIcon fontSize="small" />
                            </IconButton>
                        </div>

                        <div className="flex-1 overflow-y-auto py-3.5 space-y-2.5">
                            {messageHistory.length === 0 ? (
                                <div className="flex items-center justify-center h-full apple-text-tertiary text-xs">
                                    {localization.no_history[state.config.language]}
                                </div>
                            ) : (
                                messageHistory.map((item, index) => (
                                    <div
                                        key={index}
                                        className="p-4 rounded-2xl liquid-glass-well flex flex-col gap-1.5"
                                    >
                                        <div className="flex items-center justify-between text-[11px] font-mono apple-text-tertiary">
                                            <span>#{messageHistory.length - index}</span>
                                            <span>{formatTimestamp(item.timestamp)}</span>
                                        </div>
                                        <div className="text-xs apple-text-secondary">
                                            {item.source}
                                        </div>
                                        <div className="text-sm font-semibold apple-text-primary pt-1 border-t apple-divider">
                                            {item.translation}
                                        </div>
                                    </div>
                                ))
                            )}
                        </div>

                        <div className="pt-3 border-t apple-divider flex justify-end">
                            <Button
                                variant="contained"
                                onClick={() => setShowMessageHistory(false)}
                                className="!bg-[var(--accent-blue)] !text-white !font-semibold !px-6 !py-1.5 !rounded-full !text-xs"
                            >
                                {localization.close[state.config.language]}
                            </Button>
                        </div>
                    </div>
                </div>
            )}

            {/* Notification Snackbar */}
            <Snackbar
                open={notification.open}
                autoHideDuration={4000}
                onClose={() => setNotification(prev => ({ ...prev, open: false }))}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                TransitionComponent={Slide}
            >
                <Alert
                    onClose={() => setNotification(prev => ({ ...prev, open: false }))}
                    severity={notification.severity}
                    variant="filled"
                    sx={{
                        width: "100%",
                        bgcolor: "rgba(var(--glass-elevated-rgb), 0.92)",
                        backdropFilter: "blur(24px)",
                        color: "var(--text-primary)",
                        border: "1px solid var(--border-glass-strong)",
                        borderRadius: "14px"
                    }}
                >
                    {notification.message}
                </Alert>
            </Snackbar>
        </div>
    );
}
