using System.Reflection;
using KikitanTranslator.Utility;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class ConfigUpdate
{
    [JsonProperty("field")] public string Field;
    [JsonProperty("value")] public object Value;
}

public class UpdateConfig(Manager manager) : IHandler
{
    public async Task<string?> OnDataReceived(string data)
    {
        bool doNotRestart = false;
        
        var d = JsonConvert.DeserializeObject<ConfigUpdate>(data);
        if (d == null) return "";
        Log.Information($"[CFG]  Configuration update requested: field={d.Field}");

        switch (d.Field)
        {
            case "language":
                AppConfig.ConfigObject.Language = (string) d.Value;
                doNotRestart = true;
                
                break;
            case "source_language":
                AppConfig.ConfigObject.SourceLanguage = (string) d.Value;
                
                break;
            case "target_language":
                AppConfig.ConfigObject.TargetLanguage = (string) d.Value;
                
                break;
            case "light_mode":
                AppConfig.ConfigObject.LightMode = (bool) d.Value;
                doNotRestart = true;
                
                break;
            case "speech_to_text_only":
                AppConfig.ConfigObject.SpeechToTextOnly = (bool) d.Value;
                
                break;
            case "microphone":
                AppConfig.ConfigObject.Microphone = (string) d.Value;
                
                break;
            case "translation_only":
                AppConfig.ConfigObject.TranslationOnly = (bool) d.Value;
                
                break;
            case "disable_when_muted":
                AppConfig.ConfigObject.DisableWhenMuted = (bool) d.Value;
                
                break;
            case "send_without_waiting_for_finish":
                AppConfig.ConfigObject.SendWithoutWaitingForFinish = (bool) d.Value;
                
                break;
            case "chatbox_wait_per_char_ms":
                AppConfig.ConfigObject.ChatboxWaitPerCharMs = Convert.ToInt32((long) d.Value);
                
                break;
            case "osc_port":
                AppConfig.ConfigObject.OscPort = Convert.ToInt32((long) d.Value);
                
                break;
            case "osc_address":
                AppConfig.ConfigObject.OscAddress = (string) d.Value;
                doNotRestart = true;
                
                break;
            case "osc_secondary_port":
                AppConfig.ConfigObject.OscSecondaryPort = Convert.ToInt32((long) d.Value);
                doNotRestart = true;
                
                break;
            case "osc_enable_dual_dispatch":
                AppConfig.ConfigObject.OscEnableDualDispatch = (bool) d.Value;
                doNotRestart = true;
                
                break;
            case "send_to_chatbox":
                AppConfig.ConfigObject.SendToChatbox = (bool) d.Value;
                
                break;
            case "send_user_data":
                AppConfig.ConfigObject.SendUserData = (bool) d.Value;
                
                break;
            case "recognizer":
                AppConfig.ConfigObject.Recognizer = Convert.ToInt32((long) d.Value);

                if (AppConfig.ConfigObject.Recognizer == 2) AppConfig.ConfigObject.Translator = 2;
                else if (AppConfig.ConfigObject.Translator == 2) AppConfig.ConfigObject.Translator = 0;

                break;
            case "translator":
                var translatorIndex = Convert.ToInt32((long) d.Value);
                AppConfig.ConfigObject.Translator = translatorIndex;

                if (translatorIndex == 2) AppConfig.ConfigObject.Recognizer = 2;
                else if (AppConfig.ConfigObject.Recognizer == 2) AppConfig.ConfigObject.Recognizer = 0;

                // custom providers are picked by index in the ui but remembered by id
                if (translatorIndex >= ApiProviders.CustomIndexBase)
                {
                    var custom = AppConfig.ConfigObject.CustomProviders.ElementAtOrDefault(translatorIndex - ApiProviders.CustomIndexBase);
                    if (custom != null) AppConfig.ConfigObject.CustomProviderId = custom.Id;
                }

                break;
            case "desktop_translation":
                AppConfig.ConfigObject.DesktopTranslation = (bool) d.Value;
                
                break;
            case "quickstart_viewed":
                AppConfig.ConfigObject.QuickstartViewed = (bool) d.Value;
                doNotRestart = true;
                
                break;
            case "groq_api_key":
                AppConfig.ConfigObject.GroqApiKey = (string) d.Value;
                
                break;
            case "gemini_api_key":
                AppConfig.ConfigObject.GeminiApiKey = (string) d.Value;
                
                break;
            case "custom_providers":
                AppConfig.ConfigObject.CustomProviders = d.Value is JArray customProviders
                    ? customProviders.ToObject<List<CustomProviderConfig>>() ?? []
                    : [];

                break;
            case "custom_provider_id":
                AppConfig.ConfigObject.CustomProviderId = (string) d.Value;

                break;
            case "proxies":
                AppConfig.ConfigObject.Proxies = d.Value is JArray proxies
                    ? proxies.ToObject<List<ProxyConfig>>() ?? []
                    : [];

                break;
            case "active_proxy":
                AppConfig.ConfigObject.ActiveProxy = (string) d.Value;

                break;
            case "last_version":
                AppConfig.ConfigObject.LastVersion = (string) d.Value;
                doNotRestart = true;
                
                break;
            case "ai_semantic_correction":
                AppConfig.ConfigObject.AiSemanticCorrection = (bool) d.Value;
                doNotRestart = true;
                
                break;
            case "ai_semantic_correction_provider":
                AppConfig.ConfigObject.AiSemanticCorrectionProvider = (string) d.Value;
                doNotRestart = true;
                
                break;
            case "ai_semantic_correction_prompt":
                AppConfig.ConfigObject.AiSemanticCorrectionPrompt = (string) d.Value;
                doNotRestart = true;
                
                break;
            case "glass_opacity":
                AppConfig.ConfigObject.GlassOpacity = Convert.ToInt32(d.Value);
                doNotRestart = true;

                break;
            default:
                if (!TryApplyField(d.Field, d.Value))
                {
                    Log.Warning($"[CFG]  Received an unknown field {d.Field} with value {d.Value} while trying to update the config!");
                }

                break;
        }
        
        if (!doNotRestart) manager.RestartIfRunning();

        return "";
    }

    /// <summary>
    /// Maps the json name of every simple configuration field (API keys, models, base urls,
    /// ...) to the matching property, so that the settings page can change them without a
    /// dedicated case in the switch above.
    /// </summary>
    private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

    private static Dictionary<string, PropertyInfo> BuildFieldMap()
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in typeof(ConfigObject).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var name = field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName;
            if (string.IsNullOrEmpty(name)) continue;

            var property = typeof(ConfigObject).GetProperty(ToPascalCase(field.Name.TrimStart('_')), BindingFlags.Public | BindingFlags.Instance);
            if (property is { CanWrite: true }) map[name] = property;
            else Log.Warning($"[CFG]  The configuration field {name} has no matching property and cannot be changed from the UI");
        }

        return map;
    }

    private static bool TryApplyField(string field, object? value)
    {
        if (!FieldMap.TryGetValue(field, out var property)) return false;

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        object? converted;

        if (type == typeof(string)) converted = value?.ToString() ?? "";
        else if (type == typeof(bool)) converted = Convert.ToBoolean(value);
        else if (type == typeof(int)) converted = Convert.ToInt32(value);
        else if (type == typeof(double)) converted = Convert.ToDouble(value);
        else return false;

        property.SetValue(AppConfig.ConfigObject, converted);
        Log.Debug($"[CFG]  Configuration field {field} has been updated");

        return true;
    }

    private static string ToPascalCase(string field) => string.Concat(field
        .Split('_', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
}