import {config} from "./constants";

export type provider_def = {
    id: string;
    name: string;
    keyField: keyof config;
    modelField: keyof config;
    baseUrlField: keyof config;
    defaultBaseUrl: string;
    defaultModel: string;
    keyUrl: string;
    /// Models of this provider that always reason: thinking cannot be switched off there, so
    /// they are never picked automatically when Kikitan looks for a fast model.
    forcedThinking: string[];
};

/**
 * Every translation provider that can be configured in the "Translation API" settings page.
 * All of them are OpenAI compatible chat completion APIs (except Claude, which uses the
 * Anthropic messages API) and all of them can list their models for a given API key.
 */
export const apiProviders: provider_def[] = [
    {
        id: "groq",
        name: "Groq",
        keyField: "groq_api_key",
        modelField: "groq_model",
        baseUrlField: "groq_base_url",
        defaultBaseUrl: "https://api.groq.com/openai/v1",
        defaultModel: "qwen/qwen3.8-27b",
        keyUrl: "https://console.groq.com/keys",
        forcedThinking: ["gpt-oss"]
    },
    {
        id: "openai",
        name: "OpenAI",
        keyField: "openai_api_key",
        modelField: "openai_model",
        baseUrlField: "openai_base_url",
        defaultBaseUrl: "https://api.openai.com/v1",
        defaultModel: "gpt-4o-mini",
        keyUrl: "https://platform.openai.com/api-keys",
        forcedThinking: []
    },
    {
        id: "claude",
        name: "Claude",
        keyField: "claude_api_key",
        modelField: "claude_model",
        baseUrlField: "claude_base_url",
        defaultBaseUrl: "https://api.anthropic.com/v1",
        defaultModel: "claude-sonnet-4-5",
        keyUrl: "https://console.anthropic.com/settings/keys",
        forcedThinking: ["fable", "mythos", "opus-5.5"]
    },
    {
        id: "deepseek",
        name: "DeepSeek",
        keyField: "deepseek_api_key",
        modelField: "deepseek_model",
        baseUrlField: "deepseek_base_url",
        defaultBaseUrl: "https://api.deepseek.com",
        defaultModel: "deepseek-chat",
        keyUrl: "https://platform.deepseek.com/api_keys",
        forcedThinking: []
    },
    {
        id: "kimi",
        name: "Kimi",
        keyField: "kimi_api_key",
        modelField: "kimi_model",
        baseUrlField: "kimi_base_url",
        defaultBaseUrl: "https://api.moonshot.cn/v1",
        defaultModel: "kimi-k2.6",
        keyUrl: "https://platform.moonshot.cn/console/api-keys",
        forcedThinking: ["kimi-k3", "k2.7-code"]
    },
    {
        id: "glm",
        name: "GLM",
        keyField: "glm_api_key",
        modelField: "glm_model",
        baseUrlField: "glm_base_url",
        defaultBaseUrl: "https://open.bigmodel.cn/api/paas/v4",
        defaultModel: "glm-5.3",
        keyUrl: "https://open.bigmodel.cn/usercenter/apikeys",
        forcedThinking: ["glm-5.3"]
    }
];

export type key_only_provider = {
    id: string;
    name: string;
    keyField: keyof config;
    keyUrl: string;
    fixedModel: string;
};

/**
 * Providers that are used by a recognizer and therefore do not expose a model picker:
 * the Gemini recognizer always talks to its own live translation model.
 */
export const keyOnlyProviders: key_only_provider[] = [
    {
        id: "gemini",
        name: "Gemini",
        keyField: "gemini_api_key",
        keyUrl: "https://aistudio.google.com/api-keys",
        fixedModel: "gemini-3.5-live-translate-preview"
    }
];
const providerNames: Record<string, string> = {
    GROQ: "Groq",
    GEMINI: "Gemini",
    OPENAI: "OpenAI",
    CLAUDE: "Claude",
    DEEPSEEK: "DeepSeek",
    KIMI: "Kimi",
    GLM: "GLM"
};

/** Turns an error code such as "OPENAI" into the name that is shown to the user. */
export function providerLabel(code: string): string {
    return providerNames[code.toUpperCase()] ?? code;
}