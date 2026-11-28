// @ts-ignore
import * as React from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

import { Button, CircularProgress, IconButton } from "@mui/material";
import { Close } from '@mui/icons-material';
import { localization } from "../util/localization";
import { app_state } from "../util/constants";
import { fetchURL } from "../util/photino";

const CLOUD_CHANGELOG_BASE = "https://v4.gh-proxy.org/https://raw.githubusercontent.com/Cnfte/kikitan-translator/refs/heads/main/";

type ChangelogsProps = {
    closeCallback: () => void;
    state: app_state;
};

export default function Changelogs({ closeCallback, state }: ChangelogsProps) {
    const [changelog, setChangelog] = React.useState("");
    const [loading, setLoading] = React.useState(true);
    const [failed, setFailed] = React.useState(false);

    React.useEffect(() => {
        const file = (state.changelog_file || "CHANGELOG.md").replace(/^\/+/, "");
        const url = `${CLOUD_CHANGELOG_BASE}${file}?t=${Date.now()}`;
        setLoading(true);
        setFailed(false);
        fetchURL(url)
            .then((content) => {
                if (content && content.trim().length > 0) {
                    setChangelog(content);
                } else {
                    setFailed(true);
                }
            })
            .catch(() => setFailed(true))
            .finally(() => setLoading(false));
    }, [state.changelog_file]);

    const lang = state.config.language;

    return (
        <div className="flex flex-col h-full liquid-glass-regular font-sans select-text overflow-hidden rounded-3xl">
            {/* Header */}
            <div className="liquid-glass-clear flex items-center justify-between px-6 py-3.5 border-b apple-divider shrink-0">
                <div className="flex items-center gap-2.5">
                    <h1 className="text-sm font-bold tracking-tight apple-text-primary uppercase">
                        {localization.changelogs[lang]}
                    </h1>
                    <span className="font-mono text-[11px] apple-text-secondary liquid-glass-well border border-[var(--border-glass)] px-2.5 py-0.5 rounded-full">
                        v{state.app_version}
                    </span>
                </div>
                <IconButton
                    onClick={closeCallback}
                    size="small"
                    sx={{
                        color: "var(--text-primary)",
                        border: "1px solid var(--border-glass)",
                        borderRadius: "10px",
                        p: 0.7,
                    }}
                >
                    <Close fontSize="small" />
                </IconButton>
            </div>

            {/* Markdown Changelog Content */}
            <div className="flex-1 overflow-y-auto px-6 py-5 text-xs leading-relaxed apple-text-primary">
                {loading ? (
                    <div className="flex flex-col items-center justify-center h-full gap-3 apple-text-secondary">
                        <CircularProgress size={26} sx={{ color: "var(--accent-blue)" }} />
                        <span>正在获取云端更新日志...</span>
                    </div>
                ) : failed ? (
                    <div className="flex flex-col items-center justify-center h-full gap-2 apple-text-secondary">
                        <p className="text-sm font-medium apple-text-primary">暂时无法拉取云端更新日志</p>
                        <p className="text-xs apple-text-tertiary">请检查网络连接或代理设置后重试。</p>
                    </div>
                ) : (
                    <div className="space-y-3 [&_h1]:text-lg [&_h1]:font-bold [&_h1]:mb-3 [&_h2]:text-sm [&_h2]:font-bold [&_h2]:mt-5 [&_h2]:mb-2 [&_h2]:pb-1.5 [&_h2]:border-b [&_h2]:border-[var(--border-glass)] [&_h3]:text-xs [&_h3]:font-semibold [&_h3]:mt-3 [&_h3]:mb-1.5 [&_ul]:list-disc [&_ul]:pl-5 [&_ul]:space-y-1 [&_li]:text-xs [&_li]:leading-relaxed [&_p]:text-xs [&_p]:leading-relaxed [&_code]:font-mono [&_code]:text-[11px] [&_code]:px-1.5 [&_code]:py-0.5 [&_code]:rounded [&_code]:bg-black/15 [&_hr]:border-[var(--border-glass)] [&_hr]:my-4 [&_a]:text-[var(--accent-blue)] [&_a]:underline">
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{changelog}</ReactMarkdown>
                    </div>
                )}
            </div>

            {/* Footer */}
            <div className="liquid-glass-clear flex items-center justify-between px-6 py-3 border-t apple-divider shrink-0">
                <span className="text-[11px] apple-text-tertiary">
                    关闭后当前版本（v{state.app_version}）将不再自动弹出此窗口
                </span>
                <Button
                    variant="contained"
                    size="small"
                    onClick={closeCallback}
                    className="!bg-[var(--accent-blue)] !text-white !font-semibold !px-5 !py-1.5 !rounded-full !text-xs"
                >
                    {localization.close[lang]}
                </Button>
            </div>
        </div>
    );
}
