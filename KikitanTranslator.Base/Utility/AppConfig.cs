using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Serilog;

namespace KikitanTranslator.Utility;

public class ConfigObject : INotifyPropertyChanged
{
    [JsonProperty("quickstart_viewed")] private bool _quickstartViewed;

    [JsonIgnore]
    public bool QuickstartViewed
    {
        get => _quickstartViewed;
        set
        {
            if (_quickstartViewed != value)
            {
                _quickstartViewed = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("language")] private string _language = "en";

    [JsonIgnore]
    public string Language
    {
        get => _language;
        set
        {
            if (_language != value)
            {
                _language = value;
                OnPropertyChanged();
            }
        }
    }
    [JsonProperty("last_version")] private string _lastVersion = "en";

    [JsonIgnore]
    public string LastVersion
    {
        get => _lastVersion;
        set
        {
            if (_lastVersion != value)
            {
                _lastVersion = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("source_language")] private string _sourceLanguage = "en";

    [JsonIgnore]
    public string SourceLanguage
    {
        get => _sourceLanguage;
        set
        {
            if (_sourceLanguage != value)
            {
                _sourceLanguage = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("target_language")] private string _targetLanguage = "ja";

    [JsonIgnore]
    public string TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            if (_targetLanguage != value)
            {
                _targetLanguage = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("light_mode")] private bool _lightMode = false;

    [JsonIgnore]
    public bool LightMode
    {
        get => _lightMode;
        set
        {
            if (_lightMode != value)
            {
                _lightMode = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("speech_to_text_only")] private bool _speechToTextOnly = false;

    [JsonIgnore]
    public bool SpeechToTextOnly
    {
        get => _speechToTextOnly;
        set
        {
            if (_speechToTextOnly != value)
            {
                _speechToTextOnly = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("microphone")] private string _microphone = string.Empty;

    [JsonIgnore]
    public string Microphone
    {
        get => _microphone;
        set
        {
            if (_microphone != value)
            {
                _microphone = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("translation_only")] private bool _translationOnly = false;

    [JsonIgnore]
    public bool TranslationOnly
    {
        get => _translationOnly;
        set
        {
            if (_translationOnly != value)
            {
                _translationOnly = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("disable_when_muted")] private bool _disableWhenMuted = false;

    [JsonIgnore]
    public bool DisableWhenMuted
    {
        get => _disableWhenMuted;
        set
        {
            if (_disableWhenMuted != value)
            {
                _disableWhenMuted = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("chatbox_wait_per_char_ms")] private int _chatboxWaitPerCharMs = 30;

    [JsonIgnore]
    public int ChatboxWaitPerCharMs
    {
        get => _chatboxWaitPerCharMs;
        set
        {
            if (_chatboxWaitPerCharMs != value)
            {
                _chatboxWaitPerCharMs = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("osc_port")] private int _oscPort = 9000;

    [JsonIgnore]
    public int OscPort
    {
        get => _oscPort;
        set
        {
            if (_oscPort != value)
            {
                _oscPort = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("osc_address")] private string _oscAddress = "127.0.0.1";

    [JsonIgnore]
    public string OscAddress
    {
        get => string.IsNullOrWhiteSpace(_oscAddress) ? "127.0.0.1" : _oscAddress;
        set
        {
            if (_oscAddress != value)
            {
                _oscAddress = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("osc_secondary_port")] private int _oscSecondaryPort = 0;

    [JsonIgnore]
    public int OscSecondaryPort
    {
        get => _oscSecondaryPort;
        set
        {
            if (_oscSecondaryPort != value)
            {
                _oscSecondaryPort = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("osc_enable_dual_dispatch")] private bool _oscEnableDualDispatch = false;

    [JsonIgnore]
    public bool OscEnableDualDispatch
    {
        get => _oscEnableDualDispatch;
        set
        {
            if (_oscEnableDualDispatch != value)
            {
                _oscEnableDualDispatch = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("send_to_chatbox")] private bool _sendToChatbox = true;

    [JsonIgnore]
    public bool SendToChatbox
    {
        get => _sendToChatbox;
        set
        {
            if (_sendToChatbox != value)
            {
                _sendToChatbox = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("send_user_data")] private bool _sendUserData = false;

    [JsonIgnore]
    public bool SendUserData
    {
        get => _sendUserData;
        set
        {
            if (_sendUserData != value)
            {
                _sendUserData = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("recognizer")] private int _recognizer = 0;

    [JsonIgnore]
    public int Recognizer
    {
        get => _recognizer;
        set
        {
            if (_recognizer != value)
            {
                _recognizer = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("translator")] private int _translator = 0;

    [JsonIgnore]
    public int Translator
    {
        get => _translator;
        set
        {
            if (_translator != value)
            {
                _translator = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("desktop_translation")] private bool _desktopTranslation = false;

    [JsonIgnore]
    public bool DesktopTranslation
    {
        get => _desktopTranslation;
        set
        {
            if (_desktopTranslation != value)
            {
                _desktopTranslation = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("send_without_waiting_for_finish")] private bool _sendWithoutWaitingForFinish = false;

    [JsonIgnore]
    public bool SendWithoutWaitingForFinish
    {
        get => _sendWithoutWaitingForFinish;
        set
        {
            if (_sendWithoutWaitingForFinish != value)
            {
                _sendWithoutWaitingForFinish = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("groq_api_key")] private string _groqApiKey = "";

    [JsonIgnore]
    public string GroqApiKey
    {
        get => _groqApiKey;
        set
        {
            if (_groqApiKey != value)
            {
                _groqApiKey = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("gemini_api_key")] private string _geminiApiKey = "";

    [JsonIgnore]
    public string GeminiApiKey
    {
        get => _geminiApiKey;
        set
        {
            if (_geminiApiKey != value)
            {
                _geminiApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("groq_model")] private string _groqModel = "";

    [JsonIgnore]
    public string GroqModel
    {
        get => _groqModel;
        set
        {
            if (_groqModel != value)
            {
                _groqModel = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("groq_base_url")] private string _groqBaseUrl = "";

    [JsonIgnore]
    public string GroqBaseUrl
    {
        get => _groqBaseUrl;
        set
        {
            if (_groqBaseUrl != value)
            {
                _groqBaseUrl = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("openai_api_key")] private string _openAiApiKey = "";

    [JsonIgnore]
    public string OpenAiApiKey
    {
        get => _openAiApiKey;
        set
        {
            if (_openAiApiKey != value)
            {
                _openAiApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("openai_model")] private string _openAiModel = "";

    [JsonIgnore]
    public string OpenAiModel
    {
        get => _openAiModel;
        set
        {
            if (_openAiModel != value)
            {
                _openAiModel = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("openai_base_url")] private string _openAiBaseUrl = "";

    [JsonIgnore]
    public string OpenAiBaseUrl
    {
        get => _openAiBaseUrl;
        set
        {
            if (_openAiBaseUrl != value)
            {
                _openAiBaseUrl = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("claude_api_key")] private string _claudeApiKey = "";

    [JsonIgnore]
    public string ClaudeApiKey
    {
        get => _claudeApiKey;
        set
        {
            if (_claudeApiKey != value)
            {
                _claudeApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("claude_model")] private string _claudeModel = "";

    [JsonIgnore]
    public string ClaudeModel
    {
        get => _claudeModel;
        set
        {
            if (_claudeModel != value)
            {
                _claudeModel = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("claude_base_url")] private string _claudeBaseUrl = "";

    [JsonIgnore]
    public string ClaudeBaseUrl
    {
        get => _claudeBaseUrl;
        set
        {
            if (_claudeBaseUrl != value)
            {
                _claudeBaseUrl = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("deepseek_api_key")] private string _deepSeekApiKey = "";

    [JsonIgnore]
    public string DeepSeekApiKey
    {
        get => _deepSeekApiKey;
        set
        {
            if (_deepSeekApiKey != value)
            {
                _deepSeekApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("deepseek_model")] private string _deepSeekModel = "";

    [JsonIgnore]
    public string DeepSeekModel
    {
        get => _deepSeekModel;
        set
        {
            if (_deepSeekModel != value)
            {
                _deepSeekModel = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("deepseek_base_url")] private string _deepSeekBaseUrl = "";

    [JsonIgnore]
    public string DeepSeekBaseUrl
    {
        get => _deepSeekBaseUrl;
        set
        {
            if (_deepSeekBaseUrl != value)
            {
                _deepSeekBaseUrl = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("kimi_api_key")] private string _kimiApiKey = "";

    [JsonIgnore]
    public string KimiApiKey
    {
        get => _kimiApiKey;
        set
        {
            if (_kimiApiKey != value)
            {
                _kimiApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("kimi_model")] private string _kimiModel = "";

    [JsonIgnore]
    public string KimiModel
    {
        get => _kimiModel;
        set
        {
            if (_kimiModel != value)
            {
                _kimiModel = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("kimi_base_url")] private string _kimiBaseUrl = "";

    [JsonIgnore]
    public string KimiBaseUrl
    {
        get => _kimiBaseUrl;
        set
        {
            if (_kimiBaseUrl != value)
            {
                _kimiBaseUrl = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("glm_api_key")] private string _glmApiKey = "";

    [JsonIgnore]
    public string GlmApiKey
    {
        get => _glmApiKey;
        set
        {
            if (_glmApiKey != value)
            {
                _glmApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("glm_model")] private string _glmModel = "";

    [JsonIgnore]
    public string GlmModel
    {
        get => _glmModel;
        set
        {
            if (_glmModel != value)
            {
                _glmModel = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("glm_base_url")] private string _glmBaseUrl = "";

    [JsonIgnore]
    public string GlmBaseUrl
    {
        get => _glmBaseUrl;
        set
        {
            if (_glmBaseUrl != value)
            {
                _glmBaseUrl = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("custom_providers")] private List<CustomProviderConfig> _customProviders = [];

    [JsonIgnore]
    public List<CustomProviderConfig> CustomProviders
    {
        get => _customProviders;
        set
        {
            _customProviders = value ?? [];
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Id of the custom provider that is used when <see cref="Translator"/> is a custom index.
    /// Keeps the selection stable when the list is reordered.
    /// </summary>
    [JsonProperty("custom_provider_id")] private string _customProviderId = "";

    [JsonIgnore]
    public string CustomProviderId
    {
        get => _customProviderId;
        set
        {
            if (_customProviderId != value)
            {
                _customProviderId = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("disable_thinking")] private bool _disableThinking = true;

    [JsonIgnore]
    public bool DisableThinking
    {
        get => _disableThinking;
        set
        {
            if (_disableThinking != value)
            {
                _disableThinking = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("ai_semantic_correction")] private bool _aiSemanticCorrection = false;

    [JsonIgnore]
    public bool AiSemanticCorrection
    {
        get => _aiSemanticCorrection;
        set
        {
            if (_aiSemanticCorrection != value)
            {
                _aiSemanticCorrection = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("ai_semantic_correction_provider")] private string _aiSemanticCorrectionProvider = "auto";

    [JsonIgnore]
    public string AiSemanticCorrectionProvider
    {
        get => _aiSemanticCorrectionProvider;
        set
        {
            if (_aiSemanticCorrectionProvider != value)
            {
                _aiSemanticCorrectionProvider = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("ai_semantic_correction_prompt")] private string _aiSemanticCorrectionPrompt = "";

    [JsonIgnore]
    public string AiSemanticCorrectionPrompt
    {
        get => _aiSemanticCorrectionPrompt;
        set
        {
            if (_aiSemanticCorrectionPrompt != value)
            {
                _aiSemanticCorrectionPrompt = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("proxies")] private List<ProxyConfig> _proxies = [];

    [JsonIgnore]
    public List<ProxyConfig> Proxies
    {
        get => _proxies;
        set
        {
            _proxies = value ?? [];
            OnPropertyChanged();
        }
    }

    [JsonProperty("active_proxy")] private string _activeProxy = "";

    [JsonIgnore]
    public string ActiveProxy
    {
        get => _activeProxy;
        set
        {
            if (_activeProxy != value)
            {
                _activeProxy = value;
                OnPropertyChanged();
            }
        }
    }
    [JsonProperty("glass_opacity")] private int _glassOpacity = 55;

    [JsonIgnore]
    public int GlassOpacity
    {
        get => _glassOpacity;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (_glassOpacity != clamped)
            {
                _glassOpacity = clamped;
                OnPropertyChanged();
            }
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public delegate void OnConfigUpdate();

public static class AppConfig
{
    public static ConfigObject ConfigObject;
    public static event OnConfigUpdate? OnUpdate;
    private static string _currentConfigPath;

    public static string GetAppFolder()
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(basePath, "Kikitan Translator");
        Directory.CreateDirectory(appFolder);

        return appFolder;
    }

    public static void Load() => Load(Path.Join(GetAppFolder(), "config.json"));

    public static void Load(string configPath)
    {
        if (!Path.Exists(configPath))
        {
            Log.Warning("[CFG]  Specified path is nonexistent (perhaps first launch?). Using the default configuration");

            ConfigObject = new ConfigObject();
            ConfigObject.PropertyChanged += OnConfigPropertyChanged;
            _currentConfigPath = configPath;
            
            SaveConfig();

            return;
        }

        try
        {
            ConfigObject? cfg = JsonConvert.DeserializeObject<ConfigObject>(File.ReadAllText(configPath));
            if (cfg == null)
            {
                Log.Error("[CFG]  Deserialization result returned null!");

                return;
            }

            ConfigObject = cfg;
            ConfigObject.PropertyChanged += OnConfigPropertyChanged;
            _currentConfigPath = configPath;
            
            Log.Information($"[CFG]  Loaded from {configPath}");
        }
        catch (Exception e)
        {
            Log.Error($"[CFG]  Error occured while trying to load the config file!: {e}");

            return;
        }
    }
    
    public static void SaveConfig() {
        File.WriteAllText(_currentConfigPath, JsonConvert.SerializeObject(ConfigObject, Formatting.Indented));
        
        Log.Verbose($"[CFG]  Saved to {_currentConfigPath}");
    }

    private static void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        SaveConfig();
        
        OnUpdate?.Invoke();   
    }
}