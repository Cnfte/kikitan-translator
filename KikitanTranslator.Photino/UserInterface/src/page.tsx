import * as React from 'react';

import Kikitan from "./pages/Kikitan";

import {
    Button,
    Tooltip,
} from '@mui/material';

import {
    Settings as SettingsIcon,
    HelpOutline as HelpOutlineIcon,
    Favorite,
    DownloadOutlined,
    Clear,
    GraphicEq as GraphicEqIcon,
    Translate as TranslateIcon,
    Remove as MinimizeIcon,
    CropSquare as MaximizeIcon,
    FilterNone as RestoreIcon,
    Close as CloseIcon,
    Tune as TuneIcon,
    DarkMode as DarkModeIcon,
    LightMode as LightModeIcon,
} from '@mui/icons-material';

import Changelogs from './pages/Changelogs';
import {localization} from './util/localization';
import {
    controlKikitan,
    openURL,
    registerStateCallback,
    registerUpdateProgressCallback,
    registerWindowStateCallback,
    getWindowState,
    minimizeWindow,
    maximizeWindow,
    closeWindow,
    dragWindow,
    resizeWindow,
    sendAppState,
    sendUiDebugLog,
    setConfig,
    update
} from "./util/photino.ts";
import QuickstartMenu from "./components/Quickstart.tsx";
import SettingsPage from "./pages/Settings.tsx";
import {app_state} from "./util/constants.ts";

let updateViewed = false;

function App() {
    const [changelogsVisible, setChangelogsVisible] = React.useState(false);
    const [settingsVisible, setSettingsVisible] = React.useState(false);
    const [donateVisible, setDonateVisible] = React.useState(false);
    const [updaterVisible, setUpdaterVisible] = React.useState(false);
    const [updating, setUpdating] = React.useState(false);
    const [showProgress, setShowProgress] = React.useState(false);
    const [updatePercent, setUpdatePercent] = React.useState(0);
    const [updateError, setUpdateError] = React.useState<string | null>(null);
    const [autoCountdown, setAutoCountdown] = React.useState<number | null>(null);
    const [updaterText, setUpdaterText] = React.useState("");
    const [newVersion, setNewVersion] = React.useState("");
    const [appVersion, setAppVersion] = React.useState("");
    const [isMaximized, setIsMaximized] = React.useState(false);
    const [glassPopoverOpen, setGlassPopoverOpen] = React.useState(false);
    const [localGlassOpacity, setLocalGlassOpacity] = React.useState<number>(55);
    const changelogDismissedRef = React.useRef(false);

    // @ts-ignore
    const [appState, setAppState] = React.useState<app_state>({});
    const [loaded, setLoaded] = React.useState(false);

    const startAutoUpdateNow = React.useCallback((lang?: "en" | "jp" | "cn" | "kr") => {
        setAutoCountdown(null);
        setUpdateError(null);
        setUpdating(true);
        setShowProgress(true);
        setUpdatePercent(2);
        setUpdaterText(localization.updating[lang ?? "cn"] ?? "正在下载并自动更新...");
        update();
    }, []);

    const applyGlassCssVariables = React.useCallback((opacityValue: number, isLight: boolean) => {
        const clamped = Math.max(0, Math.min(100, opacityValue));
        const op = clamped / 100;
        const root = document.documentElement;
        root.setAttribute("data-theme", isLight ? "light" : "dark");
        root.style.setProperty("--glass-canvas-alpha", (0.03 + op * 0.82).toFixed(3));
        root.style.setProperty("--glass-surface-alpha", (0.14 + op * 0.78).toFixed(3));
        root.style.setProperty("--glass-elevated-alpha", (0.24 + op * 0.72).toFixed(3));
        root.style.setProperty("--glass-well-alpha", (0.08 + op * 0.45).toFixed(3));
        root.style.setProperty("--glass-blur", `${Math.round(12 + op * 28)}px`);
    }, []);

    React.useEffect(() => {
        setTimeout(() => setLoaded(true), 120);

        sendAppState();
        registerStateCallback(state => {
            setAppVersion(state.app_version);
            if (!updateViewed && state.server_version != null) {
                setUpdaterText(localization.new_update_available[state.config.language]);
                setNewVersion(state.server_version);
                setUpdaterVisible(true);
                setAutoCountdown(prev => (prev === null ? 3 : prev));
            }

            if (!changelogDismissedRef.current) {
                setChangelogsVisible(state.config.last_version != state.app_version);
            }
            setAppState(state);
        });

        registerUpdateProgressCallback((p) => {
            if (p.error) {
                setUpdateError(p.error);
                setUpdating(false);
                setShowProgress(false);
                setAutoCountdown(null);
                return;
            }
            setUpdatePercent(p.percent);
            if (p.status) setUpdaterText(p.status);
        });

        registerWindowStateCallback((state) => {
            setIsMaximized(state === "maximized");
        });
        getWindowState();

        if (localStorage.getItem("last_donation") == null) {
            localStorage.setItem("last_donation", "1");
        } else {
            const last = parseInt(localStorage.getItem("last_donation")!);
            if ((last + 60 * 60 * 24 * 1000) <= Date.now()) {
                setDonateVisible(true);
                localStorage.setItem("last_donation", `${Date.now()}`);
            }
        }
    }, []);

    React.useEffect(() => {
        if (appState.config != undefined) {
            const op = appState.config.glass_opacity ?? 55;
            setLocalGlassOpacity(op);
            applyGlassCssVariables(op, !!appState.config.light_mode);
        }
    }, [appState.config?.glass_opacity, appState.config?.light_mode, applyGlassCssVariables]);

    React.useEffect(() => {
        let lastSent = 0;
        let trailingTimer: number | null = null;

        const rBox = (el: Element | null | undefined) => {
            if (!el) return null;
            const r = el.getBoundingClientRect();
            return {
                x: Math.round(r.left),
                y: Math.round(r.top),
                w: Math.round(r.width),
                h: Math.round(r.height),
                sw: (el as HTMLElement).scrollWidth ?? 0,
                cw: (el as HTMLElement).clientWidth ?? 0,
            };
        };

        const emitSnapshot = (reason: string) => {
            const docEl = document.documentElement;
            const header = document.querySelector("header");
            const main = document.querySelector("main");
            const stageGrid = main?.querySelector('[data-telemetry="stage-grid"]') ?? null;
            const sections = stageGrid ? Array.from(stageGrid.querySelectorAll(":scope > section")) : [];
            const dock = main?.querySelector('[data-telemetry="control-dock"]') ?? null;

            const overflows: Array<{ tag: string; cls: string; l: number; r: number; sw: number; cw: number }> = [];
            document.querySelectorAll("header, header > div, main, section, button, input, .MuiSelect-select").forEach((el) => {
                const he = el as HTMLElement;
                const cls = typeof he.className === "string" ? he.className : "";
                if (cls.includes("MuiSelect-nativeInput")) return;
                const r = el.getBoundingClientRect();
                if (r.width > 0 && (r.right > window.innerWidth + 1 || r.left < -1 || (he.scrollWidth > he.clientWidth + 3 && getComputedStyle(he).overflowX !== "hidden"))) {
                    if (overflows.length < 6) {
                        overflows.push({
                            tag: el.tagName,
                            cls: cls.slice(0, 48),
                            l: Math.round(r.left),
                            r: Math.round(r.right),
                            sw: he.scrollWidth,
                            cw: he.clientWidth,
                        });
                    }
                }
            });

            const hBtns = header ? Array.from(header.querySelectorAll("button")) : [];
            const winBtns = hBtns.slice(-3).map((b) => rBox(b));

            const payload = {
                reason,
                vw: window.innerWidth,
                vh: window.innerHeight,
                ow: window.outerWidth,
                oh: window.outerHeight,
                dpr: Number(window.devicePixelRatio.toFixed(2)),
                docW: `${docEl.clientWidth}/${docEl.scrollWidth}`,
                hdr: rBox(header),
                hdrParts: header ? Array.from(header.children).map((c) => rBox(c)) : [],
                winBtns,
                gridCols: stageGrid ? getComputedStyle(stageGrid).gridTemplateColumns : null,
                grid: rBox(stageGrid),
                cards: sections.map((s) => rBox(s)),
                dock: rBox(dock),
                overflows,
            };
            sendUiDebugLog(JSON.stringify(payload));
        };

        const scheduleSnapshot = (reason: string) => {
            const now = Date.now();
            if (now - lastSent >= 140) {
                lastSent = now;
                emitSnapshot(reason);
            }
            if (trailingTimer != null) window.clearTimeout(trailingTimer);
            trailingTimer = window.setTimeout(() => {
                lastSent = Date.now();
                emitSnapshot(`${reason}:settled`);
            }, 220);
        };

        const onResize = () => scheduleSnapshot("window_resize");
        const onError = (e: ErrorEvent) => sendUiDebugLog(JSON.stringify({ reason: "window_error", msg: e.message, file: e.filename, line: e.lineno }));
        const onRejection = (e: PromiseRejectionEvent) => sendUiDebugLog(JSON.stringify({ reason: "unhandled_rejection", msg: String(e.reason) }));
        const onKeyDown = (e: KeyboardEvent) => {
            if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === "l") {
                openURL("LOGFILES");
            }
        };

        window.addEventListener("resize", onResize);
        window.addEventListener("error", onError);
        window.addEventListener("unhandledrejection", onRejection);
        window.addEventListener("keydown", onKeyDown);

        const ro = new ResizeObserver(() => scheduleSnapshot("ro_doc"));
        ro.observe(document.documentElement);

        const initTimer = window.setTimeout(() => emitSnapshot("mounted"), 300);

        return () => {
            window.clearTimeout(initTimer);
            if (trailingTimer != null) window.clearTimeout(trailingTimer);
            window.removeEventListener("resize", onResize);
            window.removeEventListener("error", onError);
            window.removeEventListener("unhandledrejection", onRejection);
            window.removeEventListener("keydown", onKeyDown);
            ro.disconnect();
        };
    }, []);

    React.useEffect(() => {
        if (loaded && appState.config != undefined) {
            const t = window.setTimeout(() => {
                window.dispatchEvent(new Event("resize"));
            }, 60);
            return () => window.clearTimeout(t);
        }
    }, [loaded, appState.config?.quickstart_viewed, appState.config?.light_mode, appState.config?.speech_to_text_only]);

    React.useEffect(() => {
        if (!updaterVisible || !appState.config?.quickstart_viewed || updating || updateError || autoCountdown === null) return;
        if (autoCountdown <= 0) {
            startAutoUpdateNow(appState.config.language);
            return;
        }
        const timer = window.setTimeout(() => setAutoCountdown(c => (c !== null ? c - 1 : null)), 1000);
        return () => window.clearTimeout(timer);
    }, [updaterVisible, appState.config?.quickstart_viewed, appState.config?.language, updating, updateError, autoCountdown, startAutoUpdateNow]);

    const handleOpacityChange = (val: number) => {
        const clamped = Math.max(0, Math.min(100, val));
        setLocalGlassOpacity(clamped);
        applyGlassCssVariables(clamped, !!appState.config?.light_mode);
        setConfig("glass_opacity", clamped);
    };

    const isRunning = appState.status === 2;

    return (
        <>
            {appState.config != undefined && (
                <div className={`relative h-screen overflow-hidden liquid-glass-canvas font-sans select-none transition-opacity duration-300 ${!loaded ? "opacity-0 pointer-events-none" : "opacity-100"}`}>
                    
                    {/* Frameless Edge Resize Handles */}
                    {!isMaximized && (
                        <>
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(12); }} className="fixed top-0 left-3 right-3 h-[5px] z-50 cursor-ns-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(15); }} className="fixed bottom-0 left-3 right-3 h-[5px] z-50 cursor-ns-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(10); }} className="fixed top-3 bottom-3 left-0 w-[5px] z-50 cursor-ew-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(11); }} className="fixed top-3 bottom-3 right-0 w-[5px] z-50 cursor-ew-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(13); }} className="fixed top-0 left-0 w-3 h-3 z-50 cursor-nwse-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(14); }} className="fixed top-0 right-0 w-3 h-3 z-50 cursor-nesw-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(16); }} className="fixed bottom-0 left-0 w-3 h-3 z-50 cursor-nesw-resize" />
                            <div onMouseDown={(e) => { e.stopPropagation(); resizeWindow(17); }} className="fixed bottom-0 right-0 w-3 h-3 z-50 cursor-nwse-resize" />
                        </>
                    )}

                    {/* Quickstart Modal */}
                    <div
                        className={`transition-all z-40 w-full h-screen flex backdrop-blur-md bg-black/45 justify-center items-center absolute inset-0` + (!appState.config.quickstart_viewed && appState.config.language != null ? " opacity-100" : " opacity-0 pointer-events-none")}>
                        <QuickstartMenu state={appState}></QuickstartMenu>
                    </div>

                    {/* Donation Modal */}
                    <div
                        className={'transition-all z-50 w-full h-screen flex backdrop-blur-md bg-black/45 justify-center items-center absolute inset-0' + (donateVisible && appState.config.quickstart_viewed ? " opacity-100" : " opacity-0 pointer-events-none")}>
                        <div className="flex flex-col justify-between w-[500px] max-w-[92vw] p-7 liquid-glass-sheet rounded-3xl">
                            <div className='flex flex-row justify-center'>
                                <p className='text-sm text-center leading-relaxed apple-text-primary'>{localization.donation_text[appState.config.language]}</p>
                            </div>
                            <div className='flex justify-center mt-6 gap-3 flex-wrap'>
                                <Button
                                    variant="outlined"
                                    className='!px-4 !py-2 !rounded-xl !border-[var(--border-glass-strong)] !text-[var(--text-primary)]'
                                    onClick={() => openURL("https://buymeacoffee.com/sergiomarquina")}
                                >
                                    <Favorite className='mr-1.5' fontSize="small"/>
                                    <span className='text-xs font-semibold'>Buy Me a Coffee</span>
                                </Button>
                                <Button
                                    variant="outlined"
                                    className='!px-4 !py-2 !rounded-xl !border-[var(--border-glass-strong)] !text-[var(--text-primary)]'
                                    onClick={() => openURL("https://booth.pm/en/items/6073050")}
                                >
                                    <span className='text-xs font-semibold'>Booth.pm</span>
                                </Button>
                                <Button
                                    variant="contained"
                                    className='!px-6 !py-2 !bg-[var(--accent-blue)] !text-white !font-semibold !rounded-xl'
                                    onClick={() => setDonateVisible(false)}
                                >
                                    <span className='text-xs'>{localization.close[appState.config.language]}</span>
                                </Button>
                            </div>
                        </div>
                    </div>

                    {/* Updater Modal */}
                    <div
                        className={'transition-all z-50 w-full h-screen flex backdrop-blur-md bg-black/45 justify-center items-center absolute inset-0' + (updaterVisible && appState.config.quickstart_viewed ? " opacity-100 pointer-events-auto" : " opacity-0 pointer-events-none")}>
                        <div className="flex flex-col items-center w-[460px] max-w-[92vw] p-7 liquid-glass-sheet rounded-3xl gap-4">
                            {(!appState.is_linux || appState.is_appimage) && (
                                <>
                                    <div className="flex flex-col items-center text-center">
                                        <p className="text-base font-semibold tracking-tight apple-text-primary">{updaterText}</p>
                                        <p className="text-xs font-mono apple-text-secondary mt-1.5">
                                            v{appVersion} → v{newVersion}
                                        </p>
                                        {autoCountdown !== null && !updating && !updateError && (
                                            <p className="text-[11px] apple-text-tertiary mt-1.5">
                                                将在 {autoCountdown} 秒后自动下载并更新...
                                            </p>
                                        )}
                                    </div>

                                    {showProgress && (
                                        <div className="w-full flex flex-col gap-1.5 mt-1">
                                            <div className="w-full h-2 rounded-full liquid-glass-well overflow-hidden">
                                                <div
                                                    className="h-full rounded-full bg-[var(--accent-blue)] transition-all duration-200"
                                                    style={{ width: `${Math.max(4, updatePercent)}%` }}
                                                />
                                            </div>
                                            <div className="flex items-center justify-between text-[11px] font-mono apple-text-secondary">
                                                <span>自动更新中</span>
                                                <span>{updatePercent}%</span>
                                            </div>
                                        </div>
                                    )}

                                    {updateError && (
                                        <p className="text-xs text-red-400 text-center leading-relaxed break-words">
                                            {updateError}
                                        </p>
                                    )}

                                    <div className="flex gap-3 mt-1">
                                        {!updating && (
                                            <Button
                                                variant="contained"
                                                className='!h-9 !px-5 !bg-[var(--accent-blue)] !text-white !font-semibold !rounded-xl'
                                                onClick={() => startAutoUpdateNow(appState.config.language)}
                                            >
                                                <DownloadOutlined fontSize="small" className="mr-1.5" />
                                                <span className="text-xs">{updateError ? "重试更新" : localization.update[appState.config.language]}</span>
                                            </Button>
                                        )}
                                        <Button
                                            variant="outlined"
                                            disabled={updating}
                                            className='!h-9 !px-5 !border-[var(--border-glass-strong)] !text-[var(--text-primary)] !rounded-xl'
                                            onClick={() => {
                                                updateViewed = true;
                                                setAutoCountdown(null);
                                                setUpdaterVisible(false);
                                            }}>
                                            <Clear fontSize="small" className="mr-1" />
                                            <span className="text-xs">{localization.close[appState.config.language]}</span>
                                        </Button>
                                    </div>
                                </>
                            )}
                        </div>
                    </div>

                    {/* Settings Sheet Modal */}
                    <div
                        className={'transition-all z-40 w-full h-screen flex backdrop-blur-md bg-black/40 justify-center items-center absolute inset-0' + (settingsVisible ? " opacity-100 pointer-events-auto" : " opacity-0 pointer-events-none")}>
                        <div className="flex flex-col w-[92vw] max-w-[940px] h-[84vh] rounded-3xl liquid-glass-sheet overflow-hidden">
                            <SettingsPage
                                state={appState}
                                closeCallback={() => {
                                    setSettingsVisible(false);
                                    controlKikitan(true);
                                }}
                                onUpdateFound={(remoteVersion: string) => {
                                    updateViewed = false;
                                    setUpdateError(null);
                                    setUpdating(false);
                                    setShowProgress(false);
                                    setUpdatePercent(0);
                                    setNewVersion(remoteVersion);
                                    setUpdaterText(localization.new_update_available[appState.config.language]);
                                    setAutoCountdown(3);
                                    setUpdaterVisible(true);
                                }}
                            />
                        </div>
                    </div>

                    {/* Changelogs Modal */}
                    {appState.config.quickstart_viewed && changelogsVisible && (
                        <div
                            className={'transition-all z-40 w-full h-screen flex backdrop-blur-md bg-black/40 justify-center items-center absolute inset-0' + (changelogsVisible ? " opacity-100 pointer-events-auto" : " opacity-0 pointer-events-none")}>
                            <div className="flex flex-col w-[92vw] max-w-[860px] h-[82vh] rounded-3xl liquid-glass-sheet overflow-hidden">
                                <Changelogs state={appState} closeCallback={() => {
                                    changelogDismissedRef.current = true;
                                    setConfig("last_version", appState.app_version);
                                    setChangelogsVisible(false);
                                }}/>
                            </div>
                        </div>
                    )}

                    {/* Quick Frosted Glass & Theme Popover */}
                    {glassPopoverOpen && (
                        <>
                            <div
                                className="fixed inset-0 z-30"
                                onClick={() => setGlassPopoverOpen(false)}
                            />
                            <div className="fixed top-16 right-4 z-40 w-80 p-4 rounded-2xl liquid-glass-sheet flex flex-col gap-3.5">
                                <div className="flex items-center justify-between">
                                    <span className="text-xs font-semibold tracking-tight apple-text-primary">
                                        {localization.appearance_and_glass?.[appState.config.language] ?? "外观与磨砂材质"}
                                    </span>
                                    <span className="text-[11px] font-mono font-semibold px-2 py-0.5 rounded-full liquid-glass-clear">
                                        {localGlassOpacity}%
                                    </span>
                                </div>

                                {/* Theme Segmented Switcher */}
                                <div className="grid grid-cols-2 gap-1.5 p-1 rounded-xl liquid-glass-well">
                                    <button
                                        onClick={() => setConfig("light_mode", false)}
                                        className={`flex items-center justify-center gap-1.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
                                            !appState.config.light_mode
                                                ? "bg-[var(--pill-active-bg)] text-[var(--pill-active-fg)] font-semibold shadow-sm"
                                                : "apple-text-secondary hover:apple-text-primary"
                                        }`}
                                    >
                                        <DarkModeIcon sx={{ fontSize: 14 }} />
                                        <span>{localization.theme_dark_glass?.[appState.config.language] ?? "深邃暗色"}</span>
                                    </button>
                                    <button
                                        onClick={() => setConfig("light_mode", true)}
                                        className={`flex items-center justify-center gap-1.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
                                            appState.config.light_mode
                                                ? "bg-[var(--pill-active-bg)] text-[var(--pill-active-fg)] font-semibold shadow-sm"
                                                : "apple-text-secondary hover:apple-text-primary"
                                        }`}
                                    >
                                        <LightModeIcon sx={{ fontSize: 14 }} />
                                        <span>{localization.theme_light_glass?.[appState.config.language] ?? "冰晶浅色"}</span>
                                    </button>
                                </div>

                                {/* 0 - 100 Continuous Opacity Slider */}
                                <div className="flex flex-col gap-1.5">
                                    <div className="flex items-center justify-between text-[11px] apple-text-secondary">
                                        <span>{localization.glass_opacity_label?.[appState.config.language] ?? "磨砂透度 (0 - 100)"}</span>
                                        <span className="font-mono">0 - 100</span>
                                    </div>
                                    <input
                                        type="range"
                                        min={0}
                                        max={100}
                                        value={localGlassOpacity}
                                        onChange={(e) => handleOpacityChange(parseInt(e.target.value, 10))}
                                        className="apple-slider"
                                    />
                                    <div className="flex items-center justify-between gap-1 pt-1">
                                        {[0, 25, 55, 80, 100].map((preset) => (
                                            <button
                                                key={preset}
                                                onClick={() => handleOpacityChange(preset)}
                                                className={`flex-1 py-1 rounded-lg text-[10px] font-mono transition-all ${
                                                    localGlassOpacity === preset
                                                        ? "bg-[var(--accent-blue)] text-white font-semibold"
                                                        : "liquid-glass-clear apple-text-secondary hover:apple-text-primary"
                                                }`}
                                            >
                                                {preset}%
                                            </button>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        </>
                    )}

                    {/* Main Application Layout */}
                    <div className="flex flex-col h-screen overflow-hidden px-4 pt-3 pb-3.5 gap-3">
                        
                        {/* Apple HIG Unified Top Navigation Bar */}
                        <header
                            onMouseDown={(e) => {
                                if ((e.target as HTMLElement).closest('button, a, input, select, [role="button"]')) return;
                                dragWindow();
                            }}
                            onDoubleClick={(e) => {
                                if ((e.target as HTMLElement).closest('button, a, input, select, [role="button"]')) return;
                                maximizeWindow();
                            }}
                            className="liquid-glass-regular px-3.5 md:px-4 py-2 rounded-2xl flex md:grid md:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center justify-between z-10 shrink-0 cursor-default select-none gap-2 w-full"
                        >
                            {/* Left: Brand Identity + Live Status & OSC Pill */}
                            <div className="flex items-center justify-start gap-2 min-w-0 overflow-hidden">
                                <div className="flex items-baseline gap-1.5 shrink-0">
                                    <span className="text-[15px] tracking-tight font-semibold apple-text-primary">Kikitan</span>
                                    <button
                                        onClick={() => setChangelogsVisible(true)}
                                        onMouseDown={(e) => e.stopPropagation()}
                                        className="hidden lg:inline text-[11px] font-mono apple-text-tertiary hover:apple-text-primary transition-colors cursor-pointer"
                                    >
                                        v{appVersion}
                                    </button>
                                </div>

                                <div className="hidden md:block h-3.5 w-[1px] bg-[var(--border-glass)] shrink-0" />

                                {/* Integrated Live Status Capsule */}
                                <Tooltip title={appState.is_muted
                                    ? (localization.status_muted_warning?.[appState.config.language] ?? "VRChat 静音")
                                    : isRunning
                                        ? (localization.listening_active?.[appState.config.language] ?? "监听翻译中")
                                        : (localization.listening_idle?.[appState.config.language] ?? "待机")}
                                >
                                    <div className={`flex items-center gap-1.5 px-2 py-1 md:px-2.5 md:py-0.5 rounded-full text-[11px] font-medium min-w-0 max-w-full overflow-hidden ${
                                        appState.is_muted
                                            ? "badge-pastel-yellow"
                                            : isRunning
                                                ? "badge-pastel-green"
                                                : "liquid-glass-clear apple-text-secondary"
                                    }`}>
                                        <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${
                                            appState.is_muted
                                                ? "bg-amber-400 animate-pulse"
                                                : isRunning
                                                    ? "bg-emerald-400 animate-pulse"
                                                    : "bg-zinc-400"
                                        }`} />
                                        <span className="hidden md:inline truncate min-w-0">
                                            {appState.is_muted
                                                ? (localization.status_muted_warning?.[appState.config.language] ?? "VRChat 静音")
                                                : isRunning
                                                    ? (localization.listening_active?.[appState.config.language] ?? "监听翻译中")
                                                    : (localization.listening_idle?.[appState.config.language] ?? "待机")}
                                        </span>
                                    </div>
                                </Tooltip>

                                {/* Compact OSC Port Indicator */}
                                <div
                                    className="hidden xl:flex items-center gap-1 text-[11px] font-mono apple-text-secondary px-2.5 py-0.5 rounded-full liquid-glass-clear shrink-0 whitespace-nowrap"
                                    title={`OSC: ${appState.config.osc_address || "127.0.0.1"}:${appState.config.osc_port || 9000}`}
                                >
                                    <span>OSC</span>
                                    <span className="apple-text-primary font-semibold">
                                        {appState.config.osc_port || 9000}
                                        {appState.config.osc_enable_dual_dispatch && appState.config.osc_secondary_port ? `+${appState.config.osc_secondary_port}` : ""}
                                    </span>
                                </div>
                            </div>

                            {/* Center: Apple Segmented Mode Control */}
                            <div className="shrink-0 flex items-center justify-center">
                                <div className="liquid-glass-well flex items-center p-0.5 rounded-full gap-0.5 whitespace-nowrap">
                                    <button
                                        onClick={() => setConfig("speech_to_text_only", false)}
                                        onMouseDown={(e) => e.stopPropagation()}
                                        className={`flex items-center gap-1.5 px-3 sm:px-3.5 py-1 rounded-full text-xs font-medium whitespace-nowrap transition-all cursor-pointer ${
                                            !appState.config.speech_to_text_only
                                                ? "bg-[var(--pill-active-bg)] text-[var(--pill-active-fg)] font-semibold shadow-sm"
                                                : "apple-text-secondary hover:apple-text-primary"
                                        }`}
                                    >
                                        <TranslateIcon sx={{ fontSize: 13 }} />
                                        <span>{localization.vr_mode_normal[appState.config.language] ?? "语音翻译"}</span>
                                    </button>
                                    <button
                                        onClick={() => setConfig("speech_to_text_only", true)}
                                        onMouseDown={(e) => e.stopPropagation()}
                                        className={`flex items-center gap-1.5 px-3 sm:px-3.5 py-1 rounded-full text-xs font-medium whitespace-nowrap transition-all cursor-pointer ${
                                            appState.config.speech_to_text_only
                                                ? "bg-[var(--pill-active-bg)] text-[var(--pill-active-fg)] font-semibold shadow-sm"
                                                : "apple-text-secondary hover:apple-text-primary"
                                        }`}
                                    >
                                        <GraphicEqIcon sx={{ fontSize: 13 }} />
                                        <span>{localization.vr_mode_stt_only[appState.config.language] ?? "仅转文字"}</span>
                                    </button>
                                </div>
                            </div>

                            {/* Right: Appearance Popover, Guide, Settings & Window Controls (三大金刚键) */}
                            <div 
                                className="flex items-center justify-end gap-1.5 shrink-0 z-20"
                                onMouseDown={(e) => e.stopPropagation()}
                                onDoubleClick={(e) => e.stopPropagation()}
                            >
                                <Tooltip title={localization.appearance_and_glass?.[appState.config.language] ?? "外观与磨砂透度 (0-100)"}>
                                    <button
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            setGlassPopoverOpen(!glassPopoverOpen);
                                        }}
                                        onMouseDown={(e) => e.stopPropagation()}
                                        className={`h-8 px-2 md:px-2.5 rounded-full flex items-center gap-1 text-xs font-medium transition-all cursor-pointer shrink-0 ${
                                            glassPopoverOpen
                                                ? "bg-[var(--accent-blue)] text-white"
                                                : "liquid-glass-clear apple-text-secondary hover:apple-text-primary"
                                        }`}
                                    >
                                        <TuneIcon sx={{ fontSize: 15 }} />
                                        <span className="font-mono text-[11px] hidden md:inline">{localGlassOpacity}%</span>
                                    </button>
                                </Tooltip>

                                <Tooltip title="使用指南 / Guide">
                                    <button
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            controlKikitan(false);
                                            setConfig("quickstart_viewed", false);
                                        }}
                                        onMouseDown={(e) => e.stopPropagation()}
                                        className="w-8 h-8 rounded-full liquid-glass-clear flex items-center justify-center apple-text-secondary hover:apple-text-primary transition-all cursor-pointer"
                                    >
                                        <HelpOutlineIcon sx={{ fontSize: 16 }} />
                                    </button>
                                </Tooltip>
                                
                                <Tooltip title={localization.settings[appState.config.language]}>
                                    <button
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            controlKikitan(false);
                                            setSettingsVisible(true);
                                        }}
                                        onMouseDown={(e) => e.stopPropagation()}
                                        className="w-8 h-8 rounded-full liquid-glass-clear flex items-center justify-center apple-text-secondary hover:apple-text-primary transition-all cursor-pointer"
                                    >
                                        <SettingsIcon sx={{ fontSize: 16 }} />
                                    </button>
                                </Tooltip>

                                <div className="w-[1px] h-4 bg-[var(--border-glass)] mx-0.5 shrink-0" />

                                {/* 三大金刚键: 最小化 / 最大化或还原 / 关闭 */}
                                <div 
                                    className="liquid-glass-clear flex items-center p-0.5 rounded-full gap-0.5 shrink-0"
                                    onMouseDown={(e) => e.stopPropagation()}
                                    onDoubleClick={(e) => e.stopPropagation()}
                                >
                                    <Tooltip title="最小化 / Minimize">
                                        <button
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                minimizeWindow();
                                            }}
                                            onMouseDown={(e) => e.stopPropagation()}
                                            onDoubleClick={(e) => e.stopPropagation()}
                                            className="w-7 h-7 rounded-full flex items-center justify-center apple-text-secondary hover:apple-text-primary hover:bg-black/10 dark:hover:bg-white/10 active:scale-90 transition-all cursor-pointer"
                                        >
                                            <MinimizeIcon sx={{ fontSize: 15 }} />
                                        </button>
                                    </Tooltip>
                                    
                                    <Tooltip title={isMaximized ? "向下还原 / Restore" : "最大化 / Maximize"}>
                                        <button
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                maximizeWindow();
                                            }}
                                            onMouseDown={(e) => e.stopPropagation()}
                                            onDoubleClick={(e) => e.stopPropagation()}
                                            className="w-7 h-7 rounded-full flex items-center justify-center apple-text-secondary hover:apple-text-primary hover:bg-black/10 dark:hover:bg-white/10 active:scale-90 transition-all cursor-pointer"
                                        >
                                            {isMaximized ? (
                                                <RestoreIcon sx={{ fontSize: 13 }} />
                                            ) : (
                                                <MaximizeIcon sx={{ fontSize: 13 }} />
                                            )}
                                        </button>
                                    </Tooltip>
                                    
                                    <Tooltip title="关闭 / Close">
                                        <button
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                closeWindow();
                                            }}
                                            onMouseDown={(e) => e.stopPropagation()}
                                            onDoubleClick={(e) => e.stopPropagation()}
                                            className="w-7 h-7 rounded-full flex items-center justify-center apple-text-secondary hover:!text-white hover:bg-red-500 active:scale-90 transition-all cursor-pointer"
                                        >
                                            <CloseIcon sx={{ fontSize: 14 }} />
                                        </button>
                                    </Tooltip>
                                </div>
                            </div>
                        </header>

                        {/* Main Workspace */}
                        <main className="flex-1 flex flex-col min-h-0 overflow-hidden">
                            {loaded && appState.config.quickstart_viewed && <Kikitan state={appState}/>}
                        </main>
                    </div>
                </div>
            )}
        </>
    );
}

export default App;
