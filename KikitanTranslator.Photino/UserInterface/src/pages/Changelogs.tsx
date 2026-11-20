// @ts-ignore
import * as React from "react";

import { IconButton } from "@mui/material";
import { Close } from '@mui/icons-material';
import { localization } from "../util/localization";
import { app_state } from "../util/constants";
import { fetchURL } from "../util/photino";

type ChangelogsProps = {
    closeCallback: () => void;
    state: app_state;
};

export default function Changelogs({ closeCallback, state }: ChangelogsProps) {
    const [changelog, setChangelog] = React.useState("");

    React.useEffect(() => {
        fetchURL(`https://github.com/YusufOzmen01/kikitan-translator/releases/download/v${state.app_version}/CHANGELOG_${state.config.language}.html`).then(c => setChangelog(c));
    }, []);

    return (
        <div className="flex flex-col h-full liquid-glass-regular text-white font-sans select-text overflow-hidden rounded-3xl">
            {/* Header */}
            <div className="liquid-glass-clear flex items-center justify-between px-6 py-3 border-b border-white/10 shrink-0">
                <div className="flex items-center gap-2">
                    <h1 className="text-sm font-bold tracking-tight text-white uppercase">{localization.changelogs[state.config.language]}</h1>
                    <span className="font-mono text-[10px] text-zinc-300 bg-white/10 border border-white/15 px-2 py-0.5 rounded-full">
                        v{state.app_version}
                    </span>
                </div>
                <IconButton
                    onClick={closeCallback}
                    sx={{ color: "#ffffff", border: "1px solid rgba(255,255,255,0.18)", bgcolor: "rgba(255,255,255,0.06)", borderRadius: "10px", p: 0.8, "&:hover": { bgcolor: "rgba(255,255,255,0.15)" } }}
                >
                    <Close fontSize="small" />
                </IconButton>
            </div>

            {/* Changelog Content */}
            <div
                className="flex-1 overflow-y-auto p-6 text-xs leading-relaxed text-zinc-200 space-y-2 prose prose-invert max-w-none"
                dangerouslySetInnerHTML={{ __html: changelog }}
            />
        </div>
    );
}
