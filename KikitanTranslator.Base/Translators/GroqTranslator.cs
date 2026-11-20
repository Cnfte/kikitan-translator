using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

/// <summary>
/// Groq translation is a plain OpenAI compatible chat completion, so it shares the
/// implementation with every other OpenAI compatible provider.
/// </summary>
public class GroqTranslator() : OpenAiChatTranslator(ApiProviders.Groq);