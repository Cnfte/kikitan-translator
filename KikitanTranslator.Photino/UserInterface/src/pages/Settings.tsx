import * as React from "react";

import {
    IconButton,
    TextField,
    Select,
    MenuItem,
    Button,
} from "@mui/material";

import {
    Close as CloseIcon,
    History as HistoryIcon,
    Palette as PaletteIcon,
    Translate as TranslateIcon,
    SportsEsports as VrIcon,
    Key as KeyIcon,
    Router as ProxyIcon,
    DarkMode as DarkModeIcon,
    LightMode as LightModeIcon,
} from '@mui/icons-material';

import {localization} from "../util/localization";
import {openURL, overlayAction, setConfig} from "../util/photino.ts";
import {app_state} from "../util/constants.ts";
import ApiSettings from "./settings/ApiSettings";
import ProxySettings from "./settings/ProxySettings";

type SettingsProps = {
    closeCallback: () => void;
    state: app_state;
};

function AppleSwitch({ checked, onChange }: { checked: boolean; onChange: (val: boolean) => void }) {
    return (
        <button
            type="button"
            role="switch"
            aria-checked={checked}
            data-checked={checked ? "true" : "false"}
            onClick={() => onChange(!checked)}
            className="apple-switch"
        >
            <span className="apple-switch-thumb" />
        </button>
    );
}

export default function SettingsPage({ closeCallback, state }: SettingsProps) {
    const [page, setPage] = React.useState(0);
    const [glossarySrc, setGlossarySrc] = React.useState("");
    const [glossaryTgt, setGlossaryTgt] = React.useState("");
    const lang = state.config.language;
    const customGlossary = state.config.custom_glossary ?? [];

    const handleAddGlossary = () => {
        const s = glossarySrc.trim();
        const t = glossaryTgt.trim();
        if (!s || !t) return;
        const filtered = customGlossary.filter(item => item.source.toLowerCase() !== s.toLowerCase());
        setConfig("custom_glossary", [...filtered, { source: s, target: t }]);
        setGlossarySrc("");
        setGlossaryTgt("");
    };

    const handleRemoveGlossary = (source: string) => {
        setConfig("custom_glossary", customGlossary.filter(item => item.source !== source));
    };
    const currentOpacity = state.config.glass_opacity ?? 55;

    const handleOpacitySlider = (val: number) => {
        const clamped = Math.max(0, Math.min(100, val));
        const op = clamped / 100;
        const root = document.documentElement;
        root.style.setProperty("--glass-canvas-alpha", (0.03 + op * 0.82).toFixed(3));
        root.style.setProperty("--glass-surface-alpha", (0.14 + op * 0.78).toFixed(3));
        root.style.setProperty("--glass-elevated-alpha", (0.24 + op * 0.72).toFixed(3));
        root.style.setProperty("--glass-well-alpha", (0.08 + op * 0.45).toFixed(3));
        root.style.setProperty("--glass-blur", `${Math.round(12 + op * 28)}px`);
        setConfig("glass_opacity", clamped);
    };

    const navItems = [
        {
            index: 0,
            icon: <PaletteIcon sx={{ fontSize: 17 }} />,
            label: localization.appearance_and_glass?.[lang] ?? "外观与磨砂材质",
        },
        {
            index: 1,
            icon: <TranslateIcon sx={{ fontSize: 17 }} />,
            label: localization.translation_service_settings[lang],
        },
        {
            index: 2,
            icon: <VrIcon sx={{ fontSize: 17 }} />,
            label: localization.vrchat_settings[lang],
        },
        {
            index: 3,
            icon: <KeyIcon sx={{ fontSize: 17 }} />,
            label: localization.translation_api_settings[lang],
        },
        {
            index: 4,
            icon: <ProxyIcon sx={{ fontSize: 17 }} />,
            label: `${localization.proxy_settings[lang]} / ${localization.advanced_settings[lang]}`,
        },
    ];

    return (
        <div className="flex h-full w-full font-sans select-none overflow-hidden">
            
            {/* Left Sidebar: macOS System Settings Navigation */}
            <aside className="w-44 md:w-56 shrink-0 border-r apple-divider p-3 md:p-4 flex flex-col justify-between bg-black/[0.06]">
                <div className="flex flex-col gap-1">
                    <div className="px-3 py-2 mb-1">
                        <h2 className="text-sm font-semibold tracking-tight apple-text-primary">
                            {localization.settings[lang]}
                        </h2>
                        <p className="text-[11px] apple-text-tertiary">
                            Kikitan System Preferences
                        </p>
                    </div>

                    {navItems.map((item) => {
                        const active = page === item.index;
                        return (
                            <button
                                key={item.index}
                                onClick={() => setPage(item.index)}
                                className={`w-full flex items-center gap-2.5 px-3 py-2.5 rounded-xl text-xs font-medium text-left transition-all ${
                                    active
                                        ? "bg-[var(--accent-blue)] text-white font-semibold shadow-sm"
                                        : "apple-text-secondary hover:apple-text-primary hover:bg-white/[0.06]"
                                }`}
                            >
                                {item.icon}
                                <span className="truncate">{item.label}</span>
                            </button>
                        );
                    })}
                </div>

                <div className="px-2 pt-3 border-t apple-divider flex items-center justify-between text-[11px] apple-text-tertiary">
                    <span>Liquid Glass HIG</span>
                    <span className="font-mono">v{state.app_version}</span>
                </div>
            </aside>

            {/* Right Content Area */}
            <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
                
                {/* Top Sheet Bar */}
                <div className="px-5 md:px-6 py-3.5 border-b apple-divider flex items-center justify-between shrink-0">
                    <h3 className="text-sm font-semibold apple-text-primary truncate">
                        {navItems[page]?.label}
                    </h3>

                    <IconButton
                        onClick={closeCallback}
                        size="small"
                        sx={{
                            color: "var(--text-primary)",
                            border: "1px solid var(--border-glass)",
                            bgcolor: "rgba(var(--glass-clear-rgb), 0.06)",
                            borderRadius: "9999px",
                            p: 0.7,
                        }}
                    >
                        <CloseIcon fontSize="small" />
                    </IconButton>
                </div>

                {/* Scrollable Grouped Settings Content */}
                <div className="flex-1 overflow-y-auto p-4 md:p-6">
                    
                    {/* Page 0: Appearance & Frosted Glass (0-100) */}
                    {page === 0 && (
                        <div className="flex flex-col gap-5 max-w-2xl">
                            
                            {/* Theme Material Selection */}
                            <div className="liquid-glass-regular rounded-2xl p-4 md:p-5 flex flex-col gap-4">
                                <div>
                                    <p className="text-xs font-semibold apple-text-primary">
                                        {localization.theme_mode_label?.[lang] ?? "全局磨砂主题材质"}
                                    </p>
                                    <p className="text-[11px] apple-text-secondary mt-0.5">
                                        切换深邃暗色磨砂或冰晶浅色磨砂，全界面与 Windows DWM 亚克力实时同步
                                    </p>
                                </div>

                                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                                    <button
                                        onClick={() => setConfig("light_mode", false)}
                                        className={`p-3.5 md:p-4 rounded-2xl border text-left flex items-center gap-3 transition-all ${
                                            !state.config.light_mode
                                                ? "border-[var(--accent-blue)] bg-[var(--accent-blue)]/15"
                                                : "border-[var(--border-glass)] liquid-glass-well hover:border-[var(--border-glass-strong)]"
                                        }`}
                                    >
                                        <div className="w-9 h-9 rounded-full bg-[#12141c] text-white border border-white/20 flex items-center justify-center shrink-0">
                                            <DarkModeIcon sx={{ fontSize: 18 }} />
                                        </div>
                                        <div className="min-w-0">
                                            <p className="text-xs font-semibold apple-text-primary">
                                                {localization.theme_dark_glass?.[lang] ?? "深邃暗色磨砂"}
                                            </p>
                                            <p className="text-[10px] apple-text-tertiary">Obsidian Dark Glass</p>
                                        </div>
                                    </button>

                                    <button
                                        onClick={() => setConfig("light_mode", true)}
                                        className={`p-3.5 md:p-4 rounded-2xl border text-left flex items-center gap-3 transition-all ${
                                            state.config.light_mode
                                                ? "border-[var(--accent-blue)] bg-[var(--accent-blue)]/15"
                                                : "border-[var(--border-glass)] liquid-glass-well hover:border-[var(--border-glass-strong)]"
                                        }`}
                                    >
                                        <div className="w-9 h-9 rounded-full bg-[#f4f6fb] text-[#1d1d1f] border border-black/15 flex items-center justify-center shrink-0">
                                            <LightModeIcon sx={{ fontSize: 18 }} />
                                        </div>
                                        <div className="min-w-0">
                                            <p className="text-xs font-semibold apple-text-primary">
                                                {localization.theme_light_glass?.[lang] ?? "冰晶浅色磨砂"}
                                            </p>
                                            <p className="text-[10px] apple-text-tertiary">Crystal Light Glass</p>
                                        </div>
                                    </button>
                                </div>
                            </div>

                            {/* 0 - 100 Frosted Glass Opacity Slider */}
                            <div className="liquid-glass-regular rounded-2xl p-4 md:p-5 flex flex-col gap-4">
                                <div className="flex items-center justify-between gap-2">
                                    <div>
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.glass_opacity_label?.[lang] ?? "磨砂背景透度调节 (0 - 100)"}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            {localization.glass_opacity_desc?.[lang] ?? "实时调节全主题背景与面板的液态玻璃磨砂浓度"}
                                        </p>
                                    </div>
                                    <span className="px-3 py-1 rounded-full text-xs font-mono font-semibold bg-[var(--accent-blue)] text-white shrink-0">
                                        {currentOpacity}%
                                    </span>
                                </div>

                                <div className="px-1 pt-1">
                                    <input
                                        type="range"
                                        min={0}
                                        max={100}
                                        value={currentOpacity}
                                        onChange={(e) => handleOpacitySlider(parseInt(e.target.value, 10))}
                                        className="apple-slider"
                                    />
                                    <div className="hidden md:flex justify-between text-[10px] font-mono apple-text-tertiary mt-1.5">
                                        <span>0% (全透视桌面)</span>
                                        <span>25% (轻盈磨砂)</span>
                                        <span>55% (标准均衡)</span>
                                        <span>80% (深邃磨砂)</span>
                                        <span>100% (实心高对比)</span>
                                    </div>
                                </div>

                                <div className="flex flex-wrap gap-1.5 sm:gap-2 pt-1">
                                    {[
                                        { val: 0, text: "0% 极透" },
                                        { val: 25, text: "25% 轻透" },
                                        { val: 55, text: "55% 标准" },
                                        { val: 80, text: "80% 浓厚" },
                                        { val: 100, text: "100% 实心" },
                                    ].map((preset) => (
                                        <button
                                            key={preset.val}
                                            onClick={() => handleOpacitySlider(preset.val)}
                                            className={`flex-1 min-w-[72px] py-1.5 px-2 rounded-xl text-[11px] sm:text-xs font-mono whitespace-nowrap transition-all ${
                                                currentOpacity === preset.val
                                                    ? "bg-[var(--accent-blue)] text-white font-semibold"
                                                    : "liquid-glass-clear apple-text-secondary hover:apple-text-primary"
                                            }`}
                                        >
                                            {preset.text}
                                        </button>
                                    ))}
                                </div>
                            </div>

                            {/* Interface Language */}
                            <div className="liquid-glass-regular rounded-2xl p-4 md:p-5 flex flex-wrap sm:flex-nowrap items-center justify-between gap-3">
                                <div className="min-w-0">
                                    <p className="text-xs font-semibold apple-text-primary">
                                        {localization.language_settings[lang]}
                                    </p>
                                    <p className="text-[11px] apple-text-secondary mt-0.5">
                                        Display language across all menus and controls
                                    </p>
                                </div>

                                <Select
                                    size="small"
                                    sx={{ minWidth: { xs: 130, sm: 150 }, fontSize: "0.82rem" }}
                                    value={state.config.language}
                                    onChange={(e) => setConfig("language", e.target.value)}
                                >
                                    <MenuItem value="cn">简体中文</MenuItem>
                                    <MenuItem value="en">English</MenuItem>
                                    <MenuItem value="jp">日本語</MenuItem>
                                    <MenuItem value="kr">한국어</MenuItem>
                                </Select>
                            </div>
                        </div>
                    )}

                    {/* Page 1: Speech & Translation Service Settings */}
                    {page === 1 && (
                        <div className="flex flex-col gap-5 max-w-2xl">
                            <div className="liquid-glass-regular rounded-2xl p-4 md:p-5 flex flex-col gap-4">
                                <div className="flex flex-wrap sm:flex-nowrap items-center justify-between gap-3">
                                    <div className="min-w-0">
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.translation_service[lang]}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            选择用于将原文本翻译为目标语言的引擎
                                        </p>
                                    </div>
                                    <Select
                                        size="small"
                                        sx={{ minWidth: { xs: 150, sm: 220 }, maxWidth: "100%", fontSize: "0.82rem" }}
                                        value={state.config.translator}
                                        onChange={(e) => {
                                            const value = e.target.value as number;
                                            setConfig("translator", value);
                                            if (value >= 1000) {
                                                const custom = (state.config.custom_providers ?? [])[value - 1000];
                                                if (custom != undefined) setConfig("custom_provider_id", custom.id);
                                            }
                                        }}
                                    >
                                        <MenuItem value={0}>Google Translate ({localization.default[lang]})</MenuItem>
                                        <MenuItem value={1}>Groq ({localization.requires_free_api_key[lang]})</MenuItem>
                                        <MenuItem value={2}>Gemini ({localization.requires_free_api_key[lang]})</MenuItem>
                                        <MenuItem value={3}>OpenAI ({localization.requires_paid_api_key[lang]})</MenuItem>
                                        <MenuItem value={4}>Claude ({localization.requires_paid_api_key[lang]})</MenuItem>
                                        <MenuItem value={5}>DeepSeek ({localization.requires_paid_api_key[lang]})</MenuItem>
                                        <MenuItem value={6}>Kimi ({localization.requires_paid_api_key[lang]})</MenuItem>
                                        <MenuItem value={7}>GLM ({localization.requires_paid_api_key[lang]})</MenuItem>
                                        {(state.config.custom_providers ?? []).map((provider, index) => (
                                            <MenuItem key={provider.id} value={1000 + index}>
                                                {provider.name.length > 0 ? provider.name : provider.base_url} ({localization.custom_provider_tag[lang]})
                                            </MenuItem>
                                        ))}
                                    </Select>
                                </div>

                                <div className="border-t apple-divider pt-4 flex flex-wrap sm:flex-nowrap items-center justify-between gap-3">
                                    <div className="min-w-0">
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.recognition_service[lang]}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            选择实时语音转文字识别引擎
                                        </p>
                                    </div>
                                    <Select
                                        size="small"
                                        sx={{ minWidth: { xs: 150, sm: 220 }, maxWidth: "100%", fontSize: "0.82rem" }}
                                        value={state.config.recognizer}
                                        onChange={(e) => setConfig("recognizer", e.target.value)}
                                    >
                                        <MenuItem value={0}>Microsoft Bing ({localization.default[lang]})</MenuItem>
                                        <MenuItem value={1}>Groq ({localization.requires_free_api_key[lang]})</MenuItem>
                                        <MenuItem value={2}>Gemini ({localization.requires_free_api_key[lang]})</MenuItem>
                                    </Select>
                                </div>
                            </div>

                            {/* AI Semantic Correction */}
                            <div className="liquid-glass-regular rounded-2xl p-4 md:p-5 flex flex-col gap-4">
                                <div className="flex items-center justify-between gap-3">
                                    <div className="min-w-0">
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.ai_semantic_correction?.[lang] ?? "AI 语义修正"}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            {localization.ai_semantic_correction_desc?.[lang] ?? "在语音识别后，先由AI修正同音错字与识别偏差，再进行翻译"}
                                        </p>
                                    </div>
                                    <AppleSwitch
                                        checked={!!state.config.ai_semantic_correction}
                                        onChange={(val) => setConfig("ai_semantic_correction", val)}
                                    />
                                </div>

                                <div className="border-t apple-divider pt-4 flex flex-wrap sm:flex-nowrap items-center justify-between gap-3">
                                    <span className="text-xs apple-text-secondary">
                                        {localization.ai_corrector_provider?.[lang] ?? "语义修正 AI 提供商"}
                                    </span>
                                    <Select
                                        size="small"
                                        sx={{ minWidth: { xs: 150, sm: 220 }, maxWidth: "100%", fontSize: "0.82rem" }}
                                        value={state.config.ai_semantic_correction_provider || "auto"}
                                        onChange={(e) => setConfig("ai_semantic_correction_provider", e.target.value)}
                                    >
                                        <MenuItem value="auto">{localization.auto_follow_active_or_first?.[lang] ?? "自动 (跟随当前AI翻译或首个可用API)"}</MenuItem>
                                        <MenuItem value="deepseek">DeepSeek</MenuItem>
                                        <MenuItem value="groq">Groq</MenuItem>
                                        <MenuItem value="openai">OpenAI</MenuItem>
                                        <MenuItem value="claude">Claude</MenuItem>
                                        <MenuItem value="kimi">Kimi</MenuItem>
                                        <MenuItem value="glm">GLM</MenuItem>
                                        {(state.config.custom_providers ?? []).map((provider) => (
                                            <MenuItem key={provider.id} value={`custom:${provider.id}`}>
                                                {provider.name || provider.base_url} (Custom)
                                            </MenuItem>
                                        ))}
                                    </Select>
                                </div>
                            </div>

                            {/* Custom VRChat Glossary / Name & Slang Replacements */}
                            <div className="liquid-glass-regular rounded-2xl p-4 md:p-5 flex flex-col gap-3.5">
                                <div>
                                    <p className="text-xs font-semibold apple-text-primary">
                                        {localization.custom_glossary[lang]}
                                    </p>
                                    <p className="text-[11px] apple-text-secondary mt-0.5 leading-relaxed">
                                        {localization.custom_glossary_desc[lang]}
                                    </p>
                                </div>

                                <div className="flex flex-wrap sm:flex-nowrap items-center gap-2">
                                    <TextField
                                        size="small"
                                        fullWidth
                                        placeholder={localization.glossary_source[lang]}
                                        value={glossarySrc}
                                        onChange={(e) => setGlossarySrc(e.target.value)}
                                    />
                                    <span className="text-xs apple-text-tertiary shrink-0">→</span>
                                    <TextField
                                        size="small"
                                        fullWidth
                                        placeholder={localization.glossary_target[lang]}
                                        value={glossaryTgt}
                                        onChange={(e) => setGlossaryTgt(e.target.value)}
                                        onKeyDown={(e) => { if (e.key === "Enter") handleAddGlossary(); }}
                                    />
                                    <Button
                                        size="small"
                                        variant="contained"
                                        disabled={!glossarySrc.trim() || !glossaryTgt.trim()}
                                        onClick={handleAddGlossary}
                                        className="!bg-[var(--accent-blue)] !text-white !font-semibold !rounded-xl !px-4 !h-9 !shrink-0"
                                    >
                                        {localization.glossary_add[lang]}
                                    </Button>
                                </div>

                                {customGlossary.length === 0 ? (
                                    <p className="text-[11px] apple-text-tertiary">
                                        {localization.glossary_empty[lang]}
                                    </p>
                                ) : (
                                    <div className="flex flex-wrap gap-1.5 pt-1">
                                        {customGlossary.map((entry) => (
                                            <div
                                                key={entry.source}
                                                className="flex items-center gap-1.5 px-2.5 py-1 rounded-full liquid-glass-well border border-[var(--border-glass)] text-xs"
                                            >
                                                <span className="apple-text-secondary">{entry.source}</span>
                                                <span className="apple-text-tertiary">→</span>
                                                <span className="font-semibold apple-text-primary">{entry.target}</span>
                                                <button
                                                    onClick={() => handleRemoveGlossary(entry.source)}
                                                    className="ml-0.5 apple-text-tertiary hover:text-red-400 text-xs font-bold leading-none"
                                                >
                                                    ×
                                                </button>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    )}

                    {/* Page 2: VRChat & OSC Settings */}
                    {page === 2 && (
                        <div className="flex flex-col gap-5 max-w-2xl">
                            <div className="liquid-glass-regular rounded-2xl p-5 flex flex-col gap-4">
                                <div className="flex items-center justify-between gap-4">
                                    <div>
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.only_send_translation[lang]}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            {localization.do_not_send_original_tooltip[lang]}
                                        </p>
                                    </div>
                                    <AppleSwitch
                                        checked={!!state.config.translation_only}
                                        onChange={(val) => setConfig("translation_only", val)}
                                    />
                                </div>

                                <div className="border-t apple-divider pt-4 flex items-center justify-between gap-4">
                                    <div>
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.disable_kikitan_when_muted[lang]}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            {localization.disable_kikitan_when_muted_tooltip[lang]}
                                        </p>
                                    </div>
                                    <AppleSwitch
                                        checked={!!state.config.disable_when_muted}
                                        onChange={(val) => setConfig("disable_when_muted", val)}
                                    />
                                </div>

                                <div className="border-t apple-divider pt-4 flex items-center justify-between gap-4">
                                    <div>
                                        <p className="text-xs font-semibold apple-text-primary">
                                            {localization.send_to_chatbox_without_waiting[lang]}
                                        </p>
                                        <p className="text-[11px] apple-text-secondary mt-0.5">
                                            {localization.send_to_chatbox_without_waiting_tooltip[lang]}
                                        </p>
                                    </div>
                                    <AppleSwitch
                                        checked={!!state.config.send_without_waiting_for_finish}
                                        onChange={(val) => setConfig("send_without_waiting_for_finish", val)}
                                    />
                                </div>

                                <div className="border-t apple-divider pt-4 flex flex-col gap-3.5">
                                    <div className="flex items-center justify-between gap-4">
                                        <div>
                                            <p className="text-xs font-semibold apple-text-primary">
                                                {localization.desktop_overlay_enabled[lang]}
                                            </p>
                                            <p className="text-[11px] apple-text-secondary mt-0.5">
                                                {localization.desktop_overlay_enabled_desc[lang]}
                                            </p>
                                        </div>
                                        <AppleSwitch
                                            checked={state.config.desktop_overlay_enabled ?? true}
                                            onChange={(val) => setConfig("desktop_overlay_enabled", val)}
                                        />
                                    </div>

                                    {(state.config.desktop_overlay_enabled ?? true) && (
                                        <div className="p-3.5 liquid-glass-well rounded-xl flex flex-col gap-3">
                                            <div className="flex flex-wrap sm:flex-nowrap items-center justify-between gap-3">
                                                <span className="text-xs apple-text-secondary">
                                                    {localization.overlay_position[lang]}
                                                </span>
                                                <Select
                                                    size="small"
                                                    sx={{ minWidth: 160, fontSize: "0.82rem" }}
                                                    value={state.config.overlay_position ?? 0}
                                                    onChange={(e) => setConfig("overlay_position", Number(e.target.value))}
                                                >
                                                    <MenuItem value={0}>{localization.overlay_pos_bottom_center[lang]}</MenuItem>
                                                    <MenuItem value={1}>{localization.overlay_pos_bottom_left[lang]}</MenuItem>
                                                    <MenuItem value={2}>{localization.overlay_pos_bottom_right[lang]}</MenuItem>
                                                    <MenuItem value={3}>{localization.overlay_pos_top_center[lang]}</MenuItem>
                                                    <MenuItem value={4}>{localization.overlay_pos_top_left[lang]}</MenuItem>
                                                    <MenuItem value={5}>{localization.overlay_pos_top_right[lang]}</MenuItem>
                                                </Select>
                                            </div>

                                            <div className="flex items-center justify-between gap-4">
                                                <span className="text-xs apple-text-secondary shrink-0">
                                                    {localization.overlay_scale[lang]}
                                                </span>
                                                <div className="flex items-center gap-3 flex-1 max-w-[240px]">
                                                    <input
                                                        type="range"
                                                        min={75}
                                                        max={150}
                                                        step={5}
                                                        value={state.config.overlay_scale ?? 100}
                                                        onChange={(e) => setConfig("overlay_scale", Number(e.target.value))}
                                                        className="flex-1 accent-[var(--accent-blue)] cursor-pointer"
                                                    />
                                                    <span className="font-mono text-xs font-semibold apple-text-primary w-10 text-right">
                                                        {state.config.overlay_scale ?? 100}%
                                                    </span>
                                                </div>
                                            </div>

                                            <div className="flex items-center gap-2 pt-0.5">
                                                <Button
                                                    size="small"
                                                    variant="contained"
                                                    onClick={() => overlayAction("preview")}
                                                    className="!bg-[var(--accent-blue)] !text-white !font-semibold !rounded-lg !text-xs !px-3.5"
                                                >
                                                    {localization.overlay_preview[lang]}
                                                </Button>
                                                <Button
                                                    size="small"
                                                    variant="outlined"
                                                    onClick={() => overlayAction("clear")}
                                                    sx={{ color: "var(--text-primary)", borderColor: "var(--border-glass-strong)", fontSize: "0.75rem", borderRadius: "8px", textTransform: "none" }}
                                                >
                                                    {localization.overlay_clear[lang]}
                                                </Button>
                                            </div>
                                        </div>
                                    )}
                                </div>

                                <div className="border-t apple-divider pt-4 flex items-center justify-between gap-4">
                                    <span className="text-xs font-semibold apple-text-primary">
                                        {localization.chatbox_update_speed[lang]}
                                    </span>
                                    <Select
                                        size="small"
                                        sx={{ minWidth: 150, fontSize: "0.82rem" }}
                                        value={state.config.chatbox_wait_per_char_ms}
                                        onChange={(e) => setConfig("chatbox_wait_per_char_ms", e.target.value)}
                                    >
                                        <MenuItem value={60}>{localization.slow[lang]}</MenuItem>
                                        <MenuItem value={30}>{localization.medium[lang]}</MenuItem>
                                        <MenuItem value={15}>{localization.fast[lang]}</MenuItem>
                                    </Select>
                                </div>
                            </div>

                            {/* OSC Endpoints & SlimeVR Dual Dispatch */}
                            <div className="liquid-glass-regular rounded-2xl p-5 flex flex-col gap-4">
                                <p className="text-xs font-semibold apple-text-primary">
                                    {localization.osc_address[lang]} / {localization.osc_port[lang]}
                                </p>

                                <div className="grid grid-cols-2 gap-3">
                                    <div>
                                        <label className="text-[11px] apple-text-secondary mb-1 block">
                                            {localization.osc_address[lang]}
                                        </label>
                                        <div className="flex items-center gap-1.5">
                                            <TextField
                                                size="small"
                                                fullWidth
                                                value={state.config.osc_address || "127.0.0.1"}
                                                onChange={(e) => setConfig("osc_address", e.target.value)}
                                            />
                                            <IconButton
                                                disabled={!state.config.osc_address || state.config.osc_address === "127.0.0.1"}
                                                onClick={() => setConfig("osc_address", "127.0.0.1")}
                                                size="small"
                                                sx={{ color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "10px", p: 0.8 }}
                                            >
                                                <HistoryIcon fontSize="small" />
                                            </IconButton>
                                        </div>
                                    </div>
                                    <div>
                                        <label className="text-[11px] apple-text-secondary mb-1 block">
                                            {localization.osc_port[lang]}
                                        </label>
                                        <div className="flex items-center gap-1.5">
                                            <TextField
                                                size="small"
                                                type="number"
                                                fullWidth
                                                value={state.config.osc_port || 9000}
                                                onChange={(e) => setConfig("osc_port", parseInt(e.target.value) || 9000)}
                                            />
                                            <IconButton
                                                disabled={state.config.osc_port === 9000}
                                                onClick={() => setConfig("osc_port", 9000)}
                                                size="small"
                                                sx={{ color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "10px", p: 0.8 }}
                                            >
                                                <HistoryIcon fontSize="small" />
                                            </IconButton>
                                        </div>
                                    </div>
                                </div>

                                <div className="p-4 liquid-glass-well rounded-xl flex flex-col gap-3">
                                    <div className="flex items-center justify-between gap-3">
                                        <div>
                                            <p className="text-xs font-semibold apple-text-primary">
                                                {localization.osc_secondary_port[lang]}
                                            </p>
                                            <p className="text-[11px] apple-text-secondary mt-0.5 leading-relaxed">
                                                {localization.osc_slimevr_tip[lang]}
                                            </p>
                                        </div>
                                        <AppleSwitch
                                            checked={!!state.config.osc_enable_dual_dispatch}
                                            onChange={(val) => setConfig("osc_enable_dual_dispatch", val)}
                                        />
                                    </div>

                                    <div className="flex items-center gap-2.5 pt-1">
                                        <TextField
                                            size="small"
                                            type="number"
                                            placeholder="9002"
                                            value={state.config.osc_secondary_port || ""}
                                            onChange={(e) => setConfig("osc_secondary_port", parseInt(e.target.value) || 0)}
                                            sx={{ width: 120 }}
                                        />
                                        <Button
                                            size="small"
                                            variant="outlined"
                                            onClick={() => {
                                                setConfig("osc_secondary_port", 9002);
                                                setConfig("osc_enable_dual_dispatch", true);
                                            }}
                                            sx={{
                                                color: "var(--text-primary)",
                                                borderColor: "var(--border-glass-strong)",
                                                fontSize: "0.75rem",
                                                py: 0.6,
                                                px: 2,
                                            }}
                                        >
                                            SlimeVR 9002 一键配置
                                        </Button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Page 3: Translation API */}
                    {page === 3 && <ApiSettings state={state} />}

                    {/* Page 4: Proxy & Advanced Diagnostics */}
                    {page === 4 && (
                        <div className="flex flex-col gap-5 max-w-3xl">
                            <ProxySettings state={state} />

                            <div className="liquid-glass-regular rounded-2xl p-5 flex items-center justify-between">
                                <div>
                                    <p className="text-xs font-semibold apple-text-primary">
                                        {localization.enable_user_data[lang]}
                                    </p>
                                    <p className="text-[11px] apple-text-secondary mt-0.5">
                                        Broadcast recognition & translation telemetry over UDP port 7272
                                    </p>
                                </div>
                                <div className="flex items-center gap-3">
                                    <Button
                                        variant="outlined"
                                        size="small"
                                        sx={{
                                            color: "var(--text-primary)",
                                            borderColor: "var(--border-glass-strong)",
                                            fontSize: "0.75rem",
                                        }}
                                        onClick={() => openURL("LOGFILES")}
                                    >
                                        {localization.open_logs[lang]}
                                    </Button>
                                    <AppleSwitch
                                        checked={!!state.config.send_user_data}
                                        onChange={(val) => setConfig("send_user_data", val)}
                                    />
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
