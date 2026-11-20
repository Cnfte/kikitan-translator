import * as React from "react";

import {
    Autocomplete,
    Button,
    Chip,
    CircularProgress,
    IconButton,
    MenuItem,
    Select,
    TextField,
    Tooltip,
} from "@mui/material";

import {
    Add,
    CheckCircleOutline,
    DeleteOutline,
    ErrorOutline,
    Refresh as RefreshIcon,
    TravelExplore as DetectIcon,
    VpnKey as KeyIcon,
} from "@mui/icons-material";

import {app_state, custom_provider} from "../../util/constants";
import {apiProviders} from "../../util/providers";
import {localization} from "../../util/localization";
import {modelsResponse, openURL, requestModels, setConfig} from "../../util/photino";

type provider_status = {
    loading: boolean;
    models: string[];
    error: string;
    message: string;
    detected?: string;
};

const emptyStatus: provider_status = {loading: false, models: [], error: "", message: ""};

const protocolStyles = ["openai", "claude", "gemini"];

const emptyDraft: custom_provider = {id: "", name: "", protocol: "openai", base_url: "", api_key: "", model: ""};

type card_patch = {
    apiKey?: string;
    model?: string;
    baseUrl?: string;
    protocol?: string;
};

type provider_card = {
    key: string;
    name: string;
    apiKey: string;
    model: string;
    baseUrl: string;
    defaultBaseUrl: string;
    defaultModel: string;
    showBaseUrl: boolean;
    showModel: boolean;
    modelsHelpUrl: string;
    isCustom: boolean;
    protocol?: string;
    onPatch: (patch: card_patch) => void;
    onDelete?: () => void;
};

export default function ApiSettings({state}: { state: app_state }) {
    const [statuses, setStatuses] = React.useState<Record<string, provider_status>>({});
    const [draft, setDraft] = React.useState<custom_provider>(emptyDraft);
    const [draftError, setDraftError] = React.useState("");
    const lastFetchedKey = React.useRef<Record<string, string>>({});

    const language = state.config.language;
    const customProviders: custom_provider[] = state.config.custom_providers ?? [];

    const fieldStyle = {
        '& .MuiOutlinedInput-root': {
            borderRadius: '10px',
            backgroundColor: 'rgba(var(--glass-well-rgb), var(--glass-well-alpha))',
            border: '1px solid var(--border-glass)',
        },
        '& .MuiOutlinedInput-notchedOutline': {
            border: 'none',
        },
        '&:hover .MuiOutlinedInput-root': {
            borderColor: 'var(--border-glass-strong)',
        },
        '& .MuiInputBase-input': {
            color: 'var(--text-primary)',
            fontSize: '0.84rem',
        },
        '& .MuiInputLabel-root': {
            color: 'var(--text-secondary)',
            fontSize: '0.82rem',
        },
        '& .MuiInputLabel-root.Mui-focused': {
            color: 'var(--accent-blue)',
        },
    };

    const updateCustom = (index: number, patch: card_patch) => {
        const providers = [...customProviders];
        const current = providers[index];
        if (!current) return;

        providers[index] = {
            ...current,
            api_key: patch.apiKey !== undefined ? patch.apiKey : current.api_key,
            model: patch.model !== undefined ? patch.model : current.model,
            base_url: patch.baseUrl !== undefined ? patch.baseUrl : current.base_url,
            protocol: patch.protocol !== undefined ? patch.protocol : current.protocol,
        };
        setConfig("custom_providers", providers);
    };

    const cards: provider_card[] = [
        ...apiProviders.map(info => ({
            key: info.id,
            name: info.name,
            apiKey: (state.config[info.keyField] as string) ?? "",
            model: (state.config[info.modelField] as string) ?? info.defaultModel,
            baseUrl: (state.config[info.baseUrlField] as string) ?? info.defaultBaseUrl,
            defaultBaseUrl: info.defaultBaseUrl,
            defaultModel: info.defaultModel,
            showBaseUrl: true,
            showModel: true,
            modelsHelpUrl: info.keyUrl,
            isCustom: false,
            onPatch: (patch: card_patch) => {
                if (patch.apiKey !== undefined) setConfig(info.keyField, patch.apiKey);
                if (patch.model !== undefined) setConfig(info.modelField, patch.model);
                if (patch.baseUrl !== undefined) setConfig(info.baseUrlField, patch.baseUrl);
            }
        })),
        ...customProviders.map((provider, index) => ({
            key: provider.id,
            name: provider.name.length > 0 ? provider.name : provider.base_url,
            apiKey: provider.api_key,
            model: provider.model,
            baseUrl: provider.base_url,
            defaultBaseUrl: provider.base_url,
            defaultModel: provider.model,
            showBaseUrl: true,
            showModel: true,
            modelsHelpUrl: "",
            isCustom: true,
            protocol: provider.protocol,
            onPatch: (patch: card_patch) => updateCustom(index, patch),
            onDelete: () => removeCustomProvider(provider)
        }))
    ];

    const setStatus = (key: string, patch: Partial<provider_status>) => {
        setStatuses(previous => ({
            ...previous,
            [key]: {...(previous[key] ?? emptyStatus), ...patch}
        }));
    };

    const loadModels = (card: provider_card, protocolOverride?: string) => {
        const apiKey = card.apiKey.trim();
        if (apiKey.length == 0) return;

        setStatus(card.key, {loading: true, error: "", message: ""});
        lastFetchedKey.current[card.key] = apiKey;

        requestModels(
            card.key,
            protocolOverride ?? (card.isCustom ? card.protocol : undefined),
            (response: modelsResponse) => {
                setStatus(card.key, {
                    loading: false,
                    models: response.models ?? [],
                    error: response.ok ? "" : (response.error || "failed"),
                    message: response.message ?? ""
                });
            }
        );
    };

    const detectProtocol = (card: provider_card) => {
        const apiKey = card.apiKey.trim();
        if (apiKey.length == 0) return;

        setStatus(card.key, {loading: true, error: "", message: ""});

        const attempt = (index: number) => {
            if (index >= protocolStyles.length) {
                setStatus(card.key, {loading: false, error: "detect_failed", message: ""});
                return;
            }

            const candidate = protocolStyles[index];
            requestModels(card.key, candidate, (response: modelsResponse) => {
                if (response.ok && (response.models?.length ?? 0) > 0) {
                    card.onPatch({protocol: candidate});
                    setStatus(card.key, {
                        loading: false,
                        models: response.models,
                        error: "",
                        message: "",
                        detected: candidate
                    });
                    return;
                }

                attempt(index + 1);
            });
        };

        attempt(0);
    };

    React.useEffect(() => {
        const timer = setTimeout(() => {
            cards.forEach(card => {
                const apiKey = card.apiKey.trim();
                if (apiKey.length > 0 && lastFetchedKey.current[card.key] !== apiKey) loadModels(card, card.isCustom ? card.protocol : undefined);
            });
        }, 800);

        return () => clearTimeout(timer);
    }, [state.config]);

    const addCustomProvider = () => {
        const baseUrl = draft.base_url.trim();

        if (!/^https?:\/\//i.test(baseUrl)) {
            setDraftError(localization.invalid_base_url[language]);
            return;
        }

        setDraftError("");
        setConfig("custom_providers", [...customProviders, {
            ...draft,
            id: `custom-${Date.now().toString(36)}-${Math.random().toString(36).substring(2, 6)}`,
            name: draft.name.trim(),
            base_url: baseUrl,
            api_key: draft.api_key.trim(),
            model: draft.model.trim()
        }]);
        setDraft(emptyDraft);
    };

    const removeCustomProvider = (provider: custom_provider) => {
        setConfig("custom_providers", customProviders.filter(entry => entry.id != provider.id));
        if (state.config.custom_provider_id == provider.id) setConfig("custom_provider_id", "");
    };

    const errorText = (status: provider_status) => {
        switch (status.error) {
            case "no_api_key":
                return localization.models_need_key[language];
            case "http_401":
                return localization.models_unauthorized[language];
            case "http_403":
                return localization.models_forbidden[language];
            case "http_429":
                return localization.models_rate_limited[language];
            case "empty":
                return localization.models_empty[language];
            case "detect_failed":
                return localization.detect_failed[language];
            case "unknown_provider":
                return localization.custom_provider_missing[language];
            default:
                return localization.models_failed[language].replace("{code}", status.error + (status.message.length > 0 ? `: ${status.message}` : ""));
        }
    };

    const renderStatus = (card: provider_card) => {
        const status = statuses[card.key];
        const apiKey = card.apiKey.trim();

        if (status?.loading) {
            return <Chip size="small" icon={<CircularProgress size={12}/>}
                         label={localization.models_loading[language]}/>;
        }

        if (apiKey.length == 0) {
            return <Chip size="small" label={localization.api_key_not_set[language]}/>;
        }

        if (status != undefined && status.models.length > 0) {
            return <Chip size="small" color="success" icon={<CheckCircleOutline/>}
                         label={localization.models_available[language].replace("{count}", `${status.models.length}`)}/>;
        }

        if (status != undefined && status.error.length > 0) {
            return <Tooltip title={status.message}>
                <Chip size="small" color="error" icon={<ErrorOutline/>} label={errorText(status)}/>
            </Tooltip>;
        }

        return null;
    };

    const renderCard = (card: provider_card) => {
        const status = statuses[card.key];
        const options = Array.from(new Set([
            ...(status?.models ?? []),
            ...(card.model.length > 0 ? [card.model] : []),
            ...(card.defaultModel.length > 0 ? [card.defaultModel] : [])
        ]));

        return <div key={card.key} data-testid={card.isCustom ? "custom-provider-card" : "provider-card"}
                    className="flex flex-col gap-2.5 rounded-2xl p-4 liquid-glass-regular">
            <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                    <p className="text-sm font-semibold apple-text-primary tracking-tight">{card.name}</p>
                    {card.isCustom && <span className="font-mono text-[10px] uppercase px-2 py-0.5 rounded-full liquid-glass-clear apple-text-secondary">{card.protocol}</span>}
                    {status?.detected != undefined &&
                        <span className="font-mono text-[10px] px-2 py-0.5 rounded-full badge-pastel-blue">
                            {localization.detect_success[language].replace("{style}", status.detected)}
                        </span>}
                </div>
                <div className="flex items-center gap-1.5">
                    {renderStatus(card)}
                    {card.modelsHelpUrl && card.modelsHelpUrl.length > 0 && (
                        <Tooltip title={localization.get_api_key[language]}>
                            <IconButton size="small" sx={{color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "8px", p: 0.6}}
                                        onClick={() => openURL(card.modelsHelpUrl)}>
                                <KeyIcon fontSize="small"/>
                            </IconButton>
                        </Tooltip>
                    )}
                    {card.isCustom && (
                        <Tooltip title={localization.detect_protocol[language]}>
                            <IconButton size="small" sx={{color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "8px", p: 0.6}}
                                        onClick={() => detectProtocol(card)}>
                                <DetectIcon fontSize="small"/>
                            </IconButton>
                        </Tooltip>
                    )}
                    <Tooltip title={localization.refresh_models[language]}>
                        <IconButton size="small" sx={{color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "8px", p: 0.6}}
                                    onClick={() => loadModels(card, card.isCustom ? card.protocol : undefined)}>
                            <RefreshIcon fontSize="small"/>
                        </IconButton>
                    </Tooltip>
                    {card.isCustom && card.onDelete && (
                        <Tooltip title={localization.delete[language]}>
                            <IconButton size="small" sx={{color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "8px", p: 0.6}}
                                        onClick={card.onDelete}>
                                <DeleteOutline fontSize="small"/>
                            </IconButton>
                        </Tooltip>
                    )}
                </div>
            </div>

            <div className="flex flex-col gap-2 pt-1">
                <div className="flex gap-2">
                    <TextField
                        sx={fieldStyle}
                        size="small"
                        type="password"
                        fullWidth
                        label={localization.api_key[language]}
                        value={card.apiKey}
                        onChange={(e) => card.onPatch({apiKey: e.target.value})}
                    />
                </div>

                {card.showBaseUrl && (
                    <TextField
                        sx={fieldStyle}
                        size="small"
                        type="text"
                        fullWidth
                        label={localization.base_url[language]}
                        placeholder={card.defaultBaseUrl}
                        value={card.baseUrl}
                        onChange={(e) => card.onPatch({baseUrl: e.target.value})}
                    />
                )}

                {card.showModel && (
                    <div className="flex gap-2 items-center">
                        {card.isCustom && (
                            <Select
                                sx={fieldStyle}
                                size="small"
                                className="w-36"
                                value={card.protocol ?? "openai"}
                                onChange={(e) => card.onPatch({protocol: e.target.value})}
                            >
                                <MenuItem value="openai">OpenAI</MenuItem>
                                <MenuItem value="claude">Claude</MenuItem>
                                <MenuItem value="gemini">Gemini</MenuItem>
                            </Select>
                        )}

                        <Autocomplete
                            freeSolo
                            size="small"
                            fullWidth
                            options={options}
                            value={card.model}
                            onInputChange={(_event, value) => card.onPatch({model: value})}
                            renderInput={(params) => (
                                <TextField
                                    {...params}
                                    sx={fieldStyle}
                                    label={localization.model[language]}
                                    placeholder={card.defaultModel}
                                />
                            )}
                        />
                    </div>
                )}
            </div>
        </div>;
    };

    return <div className="flex flex-col gap-4 max-w-3xl font-sans">
        {/* Thinking / Reasoning Suppression Toggle */}
        <div className="flex items-center justify-between gap-4 rounded-2xl p-5 liquid-glass-regular">
            <div>
                <p className="text-xs font-semibold apple-text-primary">{localization.disable_thinking[language]}</p>
                <p className="text-[11px] apple-text-secondary mt-0.5 leading-relaxed">{localization.disable_thinking_details[language]}</p>
            </div>
            <button
                type="button"
                role="switch"
                aria-checked={state.config.disable_thinking}
                data-checked={state.config.disable_thinking ? "true" : "false"}
                onClick={() => setConfig("disable_thinking", !state.config.disable_thinking)}
                className="apple-switch"
            >
                <span className="apple-switch-thumb" />
            </button>
        </div>

        {/* Built-in Providers */}
        <p className="text-xs font-semibold uppercase tracking-wider apple-text-secondary mt-1">{localization.translator_settings[language]}</p>
        <div className="flex flex-col gap-3">
            {cards.filter(c => !c.isCustom).map(renderCard)}
        </div>

        {/* User Added Providers */}
        <p className="text-xs font-semibold uppercase tracking-wider apple-text-secondary mt-3">{localization.custom_providers[language]}</p>
        <div className="flex flex-col gap-3">
            {cards.filter(c => c.isCustom).map(renderCard)}

            <div className="flex flex-wrap gap-2.5 items-center rounded-2xl p-4 liquid-glass-regular">
                <TextField sx={fieldStyle} size="small" label={localization.provider_name[language]} className="w-36"
                           value={draft.name} onChange={(e) => setDraft({...draft, name: e.target.value})}/>
                <Select sx={fieldStyle} size="small" className="w-32"
                        value={draft.protocol} onChange={(e) => setDraft({...draft, protocol: e.target.value})}>
                    <MenuItem value="openai">OpenAI</MenuItem>
                    <MenuItem value="claude">Claude</MenuItem>
                    <MenuItem value="gemini">Gemini</MenuItem>
                </Select>
                <TextField sx={fieldStyle} size="small" label={localization.base_url[language]} className="w-60"
                           placeholder="https://..." value={draft.base_url}
                           onChange={(e) => setDraft({...draft, base_url: e.target.value})}/>
                <TextField sx={fieldStyle} size="small" type="password" label={localization.api_key[language]}
                           className="w-48"
                           value={draft.api_key} onChange={(e) => setDraft({...draft, api_key: e.target.value})}/>
                <TextField sx={fieldStyle} size="small" label={localization.model[language]} className="w-40"
                           value={draft.model} onChange={(e) => setDraft({...draft, model: e.target.value})}/>
                <Button variant="contained" className="!h-9 !px-4 !bg-[var(--accent-blue)] !text-white !font-semibold !rounded-xl"
                        startIcon={<Add fontSize="small"/>} onClick={addCustomProvider}>
                    <span className="text-xs whitespace-nowrap">{localization.add_custom_provider[language]}</span>
                </Button>
            </div>
            {draftError.length > 0 && <p className="text-xs text-red-400">{draftError}</p>}
        </div>
    </div>;
}
