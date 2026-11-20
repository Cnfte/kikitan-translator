namespace KikitanTranslator.Utility;

public class Constants
{
    /// <summary>
    /// Velopack update feed. Leave empty to disable the self update check completely - pointing
    /// this at the original project would silently replace this build with the upstream app.
    /// Set it to your own repository (which publishes the vpk release assets) to enable updates.
    /// </summary>
    public static readonly string UPDATE_FEED_URL = "";

    public static readonly string BING_TRUSTED_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    public static readonly string BING_MS_VERSION = "1-145.0.3800.70";

    public static readonly string GROQ_PROMPT = "Translate [LANG_SRC] → [LANG_TARGET]; keep literal; output raw string only. If sent an empty text, no NOT return anything";
    public static readonly string TRANSLATION_PROMPT = "Translate the text from [LANG_SRC] to [LANG_TARGET]. Keep the original meaning, tone and formatting; reply with the translated text only (no quotes, no explanations, no extra sentences). If the text is empty, reply with nothing.";
    public static readonly string SEMANTIC_CORRECTION_PROMPT = "You are an AI speech recognition (STT) correction specialist. The user will provide speech-recognized text in language '[LANG_SRC]'. Because it was captured by automatic speech recognition, it may have homophone mistakes, transcription typos, punctuation errors, or stuttering. Your task: Correct transcription mistakes and restore what the speaker naturally intended to say in the original language '[LANG_SRC]'. Strict Rules: 1. Output ONLY the corrected text in the SAME language '[LANG_SRC]'. Do NOT translate. 2. Keep the speaker's original meaning, tone, emotion, and nuance. 3. Output ONLY the final text. Never output explanations, thinking tags, quotes, or notes. 4. If the input is already correct, return it as-is.";
    public static readonly string GROQ_MODEL = "qwen/qwen3.8-27b";
}