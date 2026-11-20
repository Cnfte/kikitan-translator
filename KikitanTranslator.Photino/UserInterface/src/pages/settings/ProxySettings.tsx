import * as React from "react";

import {
    Button,
    IconButton,
    MenuItem,
    Radio,
    Select,
    TextField,
    Tooltip,
} from "@mui/material";

import {
    Add,
    DeleteOutline,
} from "@mui/icons-material";

import {app_state, proxy_config} from "../../util/constants";
import {localization} from "../../util/localization";
import {requestProxyTest, setConfig} from "../../util/photino";

const proxyTypes = ["http", "https", "socks5", "socks4"];

type test_result = {
    loading: boolean;
    ok: boolean;
    text: string;
};

const emptyForm: proxy_config = {id: "", label: "", type: "http", host: "", port: 1080, username: "", password: ""};

export default function ProxySettings({state}: { state: app_state }) {
    const [form, setForm] = React.useState<proxy_config>(emptyForm);
    const [results, setResults] = React.useState<Record<string, test_result>>({});
    const [error, setError] = React.useState("");

    const language = state.config.language;
    const proxies = state.config.proxies ?? [];

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

    const addProxy = () => {
        if (form.host.trim().length == 0 || !form.port) {
            setError(localization.proxy_invalid[language]);
            return;
        }

        setError("");
        setConfig("proxies", [...proxies, {
            ...form,
            id: `proxy-${Date.now().toString(36)}-${Math.random().toString(36).substring(2, 6)}`,
            host: form.host.trim(),
            label: form.label.trim()
        }]);
        setForm(emptyForm);
    };

    const removeProxy = (id: string) => {
        setConfig("proxies", proxies.filter(proxy => proxy.id != id));
        if (state.config.active_proxy == id) setConfig("active_proxy", "");
    };

    const testProxy = (proxy: proxy_config) => {
        setResults(previous => ({
            ...previous,
            [proxy.id]: {loading: true, ok: false, text: localization.proxy_testing[language]}
        }));

        requestProxyTest(proxy, response => {
            setResults(previous => ({
                ...previous,
                [proxy.id]: {
                    loading: false,
                    ok: response.ok,
                    text: response.ok
                        ? localization.proxy_test_ok[language].replace("{ms}", `${response.latency_ms ?? 0}`)
                        : localization.proxy_test_failed[language].replace("{error}", response.message || response.error || "unknown error")
                }
            }));
        });
    };

    return <div className="flex flex-col gap-3 font-sans">
        <p className="text-xs apple-text-secondary">{localization.proxy_details[language]}</p>

        <div className="flex items-center rounded-2xl p-3.5 liquid-glass-regular">
            <Radio size="small" checked={state.config.active_proxy == "" || state.config.active_proxy == undefined}
                   sx={{ color: "var(--text-secondary)", "&.Mui-checked": { color: "var(--accent-blue)" } }}
                   onChange={() => setConfig("active_proxy", "")}/>
            <p className="text-xs font-semibold apple-text-primary ml-2">{localization.proxy_direct[language]}</p>
        </div>

        {proxies.length == 0 &&
            <p className="text-xs apple-text-tertiary">{localization.proxy_empty[language]}</p>}

        {proxies.map(proxy => {
            const result = results[proxy.id];

            return <div key={proxy.id}
                        className="flex flex-col gap-2 rounded-2xl p-4 liquid-glass-regular">
                <div className="flex items-center gap-2">
                    <Radio size="small" checked={state.config.active_proxy == proxy.id}
                           sx={{ color: "var(--text-secondary)", "&.Mui-checked": { color: "var(--accent-blue)" } }}
                           onChange={() => setConfig("active_proxy", proxy.id)}/>
                    <span className="font-mono text-[10px] uppercase px-2 py-0.5 rounded-full liquid-glass-clear apple-text-secondary">{proxy.type}</span>
                    <p className="text-xs font-mono font-semibold apple-text-primary">{proxy.host}:{proxy.port}</p>
                    {proxy.username.length > 0 && <p className="text-xs apple-text-secondary">{proxy.username}</p>}
                    {proxy.label.length > 0 && <p className="text-xs apple-text-secondary">({proxy.label})</p>}
                    {state.config.active_proxy == proxy.id &&
                        <span className="font-mono text-[10px] px-2 py-0.5 rounded-full badge-pastel-green">{localization.proxy_in_use[language]}</span>}
                    <div className="flex flex-1 justify-end items-center gap-2">
                        {result != undefined &&
                            <span className={`font-mono text-[10px] px-2 py-0.5 rounded-full ${result.loading ? "liquid-glass-clear apple-text-secondary" : (result.ok ? "badge-pastel-green" : "badge-pastel-red")}`}>
                                {result.text}
                            </span>}
                        <Button variant="contained" className="!h-8 !px-3.5 !bg-[var(--accent-blue)] !text-white !font-semibold !rounded-xl" disabled={result?.loading ?? false}
                                onClick={() => testProxy(proxy)}>
                            <span className="text-xs">{localization.proxy_test[language]}</span>
                        </Button>
                        <Tooltip title={localization.delete[language]}>
                            <IconButton sx={{ color: "var(--text-primary)", border: "1px solid var(--border-glass)", borderRadius: "10px", p: 0.6 }} onClick={() => removeProxy(proxy.id)}>
                                <DeleteOutline fontSize="small"/>
                            </IconButton>
                        </Tooltip>
                    </div>
                </div>
            </div>;
        })}

        <p className="text-xs font-semibold uppercase tracking-wider apple-text-secondary mt-3">{localization.proxy_add[language]}</p>
        <div className="flex flex-wrap gap-2.5 items-center rounded-2xl p-4 liquid-glass-regular">
            <Select sx={fieldStyle} size="small" className="w-28"
                    value={form.type}
                    onChange={(e) => setForm({...form, type: e.target.value})}>
                {proxyTypes.map(type => <MenuItem key={type} value={type}>
                    {type.toUpperCase()}
                </MenuItem>)}
            </Select>
            <TextField sx={fieldStyle} size="small" type="text" label={localization.proxy_host[language]}
                       className="w-52" value={form.host}
                       onChange={(e) => setForm({...form, host: e.target.value})}/>
            <TextField sx={fieldStyle} size="small" type="number" label={localization.proxy_port[language]}
                       className="w-24" value={form.port}
                       onChange={(e) => setForm({...form, port: parseInt(e.target.value) || 0})}/>
            <TextField sx={fieldStyle} size="small" type="text" label={localization.proxy_username[language]}
                       className="w-44" value={form.username}
                       onChange={(e) => setForm({...form, username: e.target.value})}/>
            <TextField sx={fieldStyle} size="small" type="password" label={localization.proxy_password[language]}
                       className="w-44" value={form.password}
                       onChange={(e) => setForm({...form, password: e.target.value})}/>
            <TextField sx={fieldStyle} size="small" type="text" label={localization.proxy_label[language]}
                       className="w-44" value={form.label}
                       onChange={(e) => setForm({...form, label: e.target.value})}/>
            <Button variant="contained" className="!h-9 !px-4 !bg-[var(--accent-blue)] !text-white !font-semibold !rounded-xl" startIcon={<Add fontSize="small"/>} onClick={addProxy}>
                <span className="text-xs whitespace-nowrap">{localization.proxy_add[language]}</span>
            </Button>
        </div>
        {error.length > 0 && <p className="text-xs text-red-400">{error}</p>}
    </div>;
}
