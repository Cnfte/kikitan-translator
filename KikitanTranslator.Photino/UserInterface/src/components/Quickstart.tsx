import * as React from "react";

import {
    GitHub,
    Translate,
    Favorite,
} from '@mui/icons-material';

import Scroll from "./Scroll";
import { localization } from "../util/localization";
import { Button, MenuItem, Select } from "@mui/material";
import {openURL, setConfig} from "../util/photino.ts";
import {app_state} from "../util/constants.ts";

export default function QuickstartMenu({ state }: { state: app_state }) {
    const [quickstartPage, setQuickstartPage] = React.useState(0);

    return (
        <div className="flex flex-col justify-between w-11/12 max-w-[760px] h-[540px] max-h-[84vh] liquid-glass-regular rounded-3xl border border-white/16 shadow-[0_24px_64px_rgba(0,0,0,0.65),inset_0_1px_1px_rgba(255,255,255,0.22)] p-6 md:p-7 select-none font-sans overflow-hidden">
            <div className="relative flex-1 flex flex-col justify-center items-center">
                
                {/* Page 0: Language Select */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col justify-center items-center ${quickstartPage === 0 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    {quickstartPage === 0 && !state.config.quickstart_viewed && <Scroll light_mode={false}></Scroll>}
                    
                    <div className="flex items-center gap-3 mt-8">
                        <Translate sx={{ fontSize: 20, color: "#ffffff" }} />
                        <Select
                            size="small"
                            sx={{
                                color: "#ffffff",
                                bgcolor: "rgba(0, 0, 0, 0.35)",
                                minWidth: 150,
                                fontSize: "0.85rem",
                                borderRadius: "10px",
                                border: "1px solid rgba(255, 255, 255, 0.14)",
                                "& .MuiOutlinedInput-notchedOutline": { border: "none" },
                            }}
                            value={state.config.language}
                            onChange={(e) => setConfig("language", e.target.value)}
                        >
                            <MenuItem value={"en"}>English</MenuItem>
                            <MenuItem value={"jp"}>日本語</MenuItem>
                            <MenuItem value={"cn"}>中文</MenuItem>
                            <MenuItem value={"kr"}>한국어</MenuItem>
                        </Select>
                    </div>
                </div>

                {/* Page 1: OSC setup */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col items-center justify-center ${quickstartPage === 1 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    <p className="text-base font-semibold text-white tracking-tight mb-3">{localization.quickstart_osc[state.config.language]}</p>
                    {quickstartPage === 1 && (
                        <video className="w-full max-w-[420px] rounded-xl border border-white/12 shadow-[0_8px_32px_rgba(0,0,0,0.35)]" autoPlay loop>
                            <source src="/OSC.mp4" type="video/mp4"></source>
                        </video>
                    )}
                </div>

                {/* Page 2: Mode Selection */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col items-center justify-center ${quickstartPage === 2 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    <p className="text-base font-semibold text-white tracking-tight mb-2">{localization.mode_selection[state.config.language]}</p>
                    <img className="w-full max-w-[340px] rounded-xl border border-white/12 shadow-[0_8px_32px_rgba(0,0,0,0.35)]" src={
                        {
                            en: "/images/en.png",
                            jp: "/images/jp.png",
                            cn: "/images/cn.png",
                            kr: "/images/kr.png",
                        }[state.config.language]
                    } />
                    <p className="text-xs text-zinc-300 mt-3 text-center max-w-md leading-relaxed">{localization.mode_selection_details[state.config.language]}</p>
                </div>

                {/* Page 3: Desktop Audio */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col items-center justify-center ${quickstartPage === 3 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    <p className="text-base font-semibold text-white tracking-tight mb-2">{localization.desktop_translation[state.config.language]}</p>
                    <img className="w-full max-w-[340px] rounded-xl border border-white/12 shadow-[0_8px_32px_rgba(0,0,0,0.35)]" src={
                        {
                            en: "/images/en2.png",
                            jp: "/images/jp2.png",
                            cn: "/images/cn2.png",
                            kr: "/images/kr2.png",
                        }[state.config.language]
                    } />
                    <p className="text-xs text-zinc-300 mt-3 text-center max-w-md leading-relaxed">{localization.desktop_translation_details[state.config.language]}</p>
                </div>

                {/* Page 4: Settings Intro */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col items-center justify-center ${quickstartPage === 4 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    <p className="text-base font-semibold text-white tracking-tight mb-2">{localization.settings[state.config.language]}</p>
                    <img className="w-full max-w-[340px] rounded-xl border border-white/12 shadow-[0_8px_32px_rgba(0,0,0,0.35)]" src="/images/settings.png" />
                    <p className="text-xs text-zinc-300 mt-3 text-center max-w-md leading-relaxed">{localization.settings_details[state.config.language]}</p>
                </div>

                {/* Page 5: Theme */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col items-center justify-center ${quickstartPage === 5 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    <p className="text-base font-semibold text-white tracking-tight mb-2">{localization.change_theme[state.config.language]}</p>
                    {quickstartPage === 5 && (
                        <video className="w-full max-w-[340px] rounded-xl border border-white/12 shadow-[0_8px_32px_rgba(0,0,0,0.35)]" autoPlay loop>
                            <source src="/theme.mp4" type="video/mp4"></source>
                        </video>
                    )}
                    <p className="text-xs text-zinc-300 mt-3 text-center max-w-md leading-relaxed">{localization.change_theme_detail[state.config.language]}</p>
                </div>

                {/* Page 6: Finished */}
                <div className={`absolute inset-0 transition-opacity duration-200 flex flex-col items-center justify-center ${quickstartPage === 6 ? "opacity-100" : "opacity-0 pointer-events-none"}`}>
                    <p className="text-lg font-bold text-white tracking-tight mb-2">{localization.thank_you[state.config.language]}</p>
                    <p className="text-xs text-zinc-300 text-center max-w-md mb-6 leading-relaxed">{localization.thank_you_details[state.config.language]}</p>
                    
                    <div className="flex gap-3 flex-wrap justify-center">
                        <Button
                            variant="outlined"
                            className="!px-4 !py-2.5 !border-white/20 !text-white hover:!bg-white/10 !rounded-xl"
                            startIcon={<GitHub fontSize="small" />}
                            onClick={() => openURL("https://github.com/YusufOzmen01/kikitan-translator")}
                        >
                            <span className="text-xs font-semibold">{localization.open_repo[state.config.language]}</span>
                        </Button>
                        <Button
                            variant="outlined"
                            className="!px-4 !py-2.5 !border-white/20 !text-white hover:!bg-white/10 !rounded-xl"
                            startIcon={<Favorite fontSize="small" />}
                            onClick={() => openURL("https://buymeacoffee.com/sergiomarquina")}
                        >
                            <span className="text-xs font-semibold">Buy Me a Coffee</span>
                        </Button>
                        <Button
                            variant="outlined"
                            className="!px-4 !py-2.5 !border-white/20 !text-white hover:!bg-white/10 !rounded-xl"
                            onClick={() => openURL("https://booth.pm/en/items/6073050")}
                        >
                            <img src="/boothlogo.svg" width={16} className="mr-1.5 invert" alt="Booth" />
                            <span className="text-xs font-semibold">Booth.pm</span>
                        </Button>
                    </div>
                </div>
            </div>

            {/* Pagination Controls */}
            <div className="pt-4 border-t border-white/10 flex items-center justify-between">
                <Button
                    variant="outlined"
                    disabled={quickstartPage === 0}
                    className="!px-4 !py-2 !border-white/20 !text-white !rounded-xl !text-xs disabled:!opacity-30"
                    onClick={() => setQuickstartPage(quickstartPage - 1)}
                >
                    {localization.previous[state.config.language]}
                </Button>

                <div className="flex items-center gap-1.5">
                    {[0, 1, 2, 3, 4, 5, 6].map(i => (
                        <span
                            key={i}
                            className={`h-1.5 rounded-full transition-all ${
                                quickstartPage === i ? "bg-white w-4" : "bg-white/20 w-1.5"
                            }`}
                        />
                    ))}
                </div>

                <Button
                    variant="contained"
                    className="!px-6 !py-2 !bg-white !text-black !font-semibold hover:!bg-zinc-200 !rounded-xl !text-xs shadow-[0_2px_10px_rgba(255,255,255,0.2)]"
                    onClick={() => {
                        if (quickstartPage >= 6) {
                            setConfig("quickstart_viewed", true);
                            setQuickstartPage(0);
                        } else {
                            setQuickstartPage(quickstartPage + 1);
                        }
                    }}
                >
                    {quickstartPage >= 6 ? localization.close[state.config.language] : localization.next[state.config.language]}
                </Button>
            </div>
        </div>
    );
}
