using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using KikitanTranslator.Base;
using KikitanTranslator.Base.Outputs;
using KikitanTranslator.Base.Recognizers;
using KikitanTranslator.Base.Translators;
using KikitanTranslator.Capture;
using KikitanTranslator.Photino.Handlers;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;
using Newtonsoft.Json;
using Photino.NET;
using Serilog;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Structs;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace KikitanTranslator.Photino;

public class Mic
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("default")] public bool Default;
}

public class AppState
{
    [JsonProperty("microphones")] public Mic[] Microphones;
    [JsonProperty("config")] public ConfigObject Config;
    [JsonProperty("status")] public int Status;
    [JsonProperty("app_version")] public string AppVersion = "2.3.0";
    [JsonProperty("server_version")] public string? ServerVersion;
    [JsonProperty("changelog_file")] public string ChangelogFile = Constants.DEFAULT_CHANGELOG_FILE;
    [JsonProperty("is_linux")] public bool IsLinux;
    [JsonProperty("is_appimage")] public bool IsAppimage;
    [JsonProperty("is_muted")] public bool IsMuted;
}

public class RecognitionData
{
    [JsonProperty("transcription")] public string Transcription;
    [JsonProperty("translation")] public string Translation;
    [JsonProperty("final")] public bool Final;
}

public class Manager
{
    private Kikitan? _microphoneKikitan;
    private Kikitan? _desktopKikitan;

    private ITranslator _translator;
    private Loopback _loopback;
    private Microphone _mic;
    private AppState _appState = new () { Microphones = [] };
    private IErrorHandler _errorHandler;
    private volatile bool _running;

    private OverlayWriter? _writer;
    private readonly object _overlayStateLock = new();
    private long _lastOtherInterimOverlayWriteTick;
    private long _lastSelfInterimOverlayWriteTick;

    private CloudUpdateInfo? _pendingUpdate;
    private int _updateInProgress;

    private Connector _connector;

    public DeviceInfo[] GetMicrophones() => _mic.GetCaptureDevices();

    /// <summary>
    /// An overlay process that survived an unclean shutdown keeps the memory mapped pipe open,
    /// which would make the next start fail, so leftovers are stopped before a new one starts.
    /// </summary>
    private static void StopLeftoverOverlays()
    {
        foreach (var process in Process.GetProcessesByName("KikitanTranslator.Overlay"))
        {
            try
            {
                Log.Information($"[APP] Stopping the leftover overlay process {process.Id}");
                process.Kill();
                process.WaitForExit(3000);
            }
            catch (Exception e)
            {
                Log.Warning($"[APP] Could not stop the overlay process {process.Id}: {e.Message}");
            }
        }
    }

    public Manager(bool noUI, Connector connector)
    {
        _appState.Config = AppConfig.ConfigObject;

        _errorHandler = new ErrorHandler(connector);
        
        _appState.AppVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Split('+')[0]
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
            ?? "2.3.0";

        #if DEBUG
        _loopback = new("Resources/wwwroot/silero_vad.onnx");
        _mic = new("Resources/wwwroot/silero_vad.onnx", _errorHandler);
        #else
        _loopback = new(Path.Combine(AppContext.BaseDirectory, "wwwroot", "silero_vad.onnx"));
        _mic = new(Path.Combine(AppContext.BaseDirectory, "wwwroot", "silero_vad.onnx"), _errorHandler);
        #endif
        
        _appState.IsLinux = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _appState.IsAppimage = _appState.IsLinux && Environment.GetEnvironmentVariable("KIKITAN_NOT_APPIMAGE") == null;
        _connector = connector;

        // Cloud update check via gx.txt (VERSION + RELEASES_URL)
        Task.Run(async () =>
        {
            var updateInfo = await CloudUpdater.CheckForUpdateAsync(_appState.AppVersion);
            if (updateInfo != null)
            {
                _pendingUpdate = updateInfo;
                _appState.ServerVersion = updateInfo.RemoteVersion;
                _appState.ChangelogFile = updateInfo.ChangelogFile;
                SendUpdateToUI();
            }
        });
        
        AppConfig.OnUpdate += () =>
        {
            _appState.Config = AppConfig.ConfigObject;

            SyncOverlayTheme();
            SendUpdateToUI();
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !noUI)
        {
            StopLeftoverOverlays();

            try
            {
                _writer = new();
                SyncOverlayTheme();
            }
            catch (Exception e)
            {
                Log.Error(e, "[APP] Unable to open the overlay pipe; the desktop overlay will not work");
            }
            
            if (!Path.Exists("KikitanTranslator.Overlay.exe"))
            {
                Log.Warning("Kikitan Overlay doesn't exist! Perhaps a debug build?");
            }
            else
            {
                Process proc = new Process();
                proc.StartInfo.FileName = "KikitanTranslator.Overlay.exe";
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.UseShellExecute = false;
                proc.Start();
            }
        }

        try
        {
            var oscWatcher = new OscWatcher();
            oscWatcher.MuteStatusChanged += muted =>
            {
                _appState.IsMuted = muted;
                
                SendUpdateToUI();
            };
            oscWatcher.Start();
        }
        catch (Exception ex)
        {
            Log.Warning($"[APP] OscWatcher failed to start: {ex.Message}");
        }
        
        Task.Run(async () =>
        {
            var engine = new MiniAudioEngine(backendPriority: [MiniAudioBackend.Wasapi, MiniAudioBackend.Oss]);

            List<Mic> mics = new();

            while (true)
            {
                engine.UpdateAudioDevicesInfo();
                foreach (var mic in engine.CaptureDevices)
                {
                    mics.Add(new Mic { Name = mic.Name, Default = mic.IsDefault });
                }
                
                if (_appState.Microphones.Length != 0 && mics.Count != _appState.Microphones.Length)
                {
                    _appState.Microphones = mics.ToArray();
                    
                    SendUpdateToUI();
                }

                if (_appState.Config.Microphone.Length != 0 && !mics.Exists(m => m.Name == _appState.Config.Microphone) && engine.CaptureDevices.Length > 0)
                {
                    var device = engine.CaptureDevices.Any(d => d.IsDefault)
                        ? engine.CaptureDevices.First(d => d.IsDefault)
                        : engine.CaptureDevices[0];
                    
                    Log.Warning($"[MIC]  The selected mic ({AppConfig.ConfigObject.Microphone}) is not available. Switching to the system default ({device.Name})");
                    
                    AppConfig.ConfigObject.Microphone = device.Name;
                    
                    SendMicChanged();
                    RestartIfRunning();
                }
                
                
                _appState.Microphones = mics.ToArray();
                
                mics.Clear();

                await Task.Delay(500);
            }
        });
    }

    public void Start()
    {
        Log.Information($"[APP] Start requested: running={_running}, recognizer={AppConfig.ConfigObject.Recognizer}, translator={AppConfig.ConfigObject.Translator}, desktop={AppConfig.ConfigObject.DesktopTranslation}, chatbox={AppConfig.ConfigObject.SendToChatbox}");

        if (_running)
        {
            RestartIfRunning();

            return;
        }

        IRecognizer rMic = CreateRecognizer(_mic);

        var translator = CreateTranslator();
        if (translator == null)
        {
            Log.Error($"[APP] Unable to start: the configured translator ({AppConfig.ConfigObject.Translator}) is not usable");

            return;
        }

        _translator = translator;

        _running = true;

        _microphoneKikitan = new Kikitan(rMic, _translator, new ErrorHandler(_connector), false);
        _microphoneKikitan.AddOutput(new Custom((recognized, translated, final) =>
        {
            if (!_running) return;
            SendRecognitionData(recognized, translated, final);
            UpdateOverlaySpeaker(isOther: false, recognized, translated, final);
        }, false));
        if (AppConfig.ConfigObject.SendToChatbox)
        {
            var chatbox = new Chatbox();
            
            _microphoneKikitan.AddOutput(new Custom((r, t, f) =>
            {
                if (!_running) return;
                if (AppConfig.ConfigObject.DisableWhenMuted && _appState.IsMuted)
                {
                    if (f) Log.Debug("[OSC]  Final chatbox output skipped: VRChat microphone is muted");
                    return;
                }
                
                chatbox.Send(r, t, f);
            }, true));
        }
        
        if (AppConfig.ConfigObject.SendUserData)
        {
            _microphoneKikitan.AddOutput(new OSC("/microphone"));
        }
        
        _microphoneKikitan.OnRecognizerStatusChanged += s =>
        {
            _appState.Status = (int)s;
            
            SendUpdateToUI();
        };
        
        _microphoneKikitan.Start();

        if (AppConfig.ConfigObject.DesktopTranslation && !_appState.IsLinux)
        {
            IRecognizer rDesktop = CreateRecognizer(_loopback);
            
            _desktopKikitan = new Kikitan(rDesktop, _translator, new ErrorHandler(_connector), true);
            Log.Information($"[APP] Desktop pipeline starting: recognizer={rDesktop.GetType().Name}, overlayAvailable={_writer != null}");
            if (_writer != null)
            {
                _desktopKikitan.AddOutput(new Custom((recognized, translated, final) =>
                {
                    if (!_running) return;
                    UpdateOverlaySpeaker(isOther: true, recognized, translated, final);
                }, false));
            }
            
            if (AppConfig.ConfigObject.SendUserData)
            {
                _desktopKikitan.AddOutput(new OSC("/desktop"));
            }
            
            _desktopKikitan?.Start();
        }
        
        Log.Information($"[APP] Startup complete.");
        SendUpdateToUI();
    }

    private static IRecognizer CreateRecognizer(ICapture capture) => AppConfig.ConfigObject.Recognizer switch
    {
        1 => new GroqRecognizer(capture),
        2 => new Gemini(capture),
        _ => new Bing(capture),
    };

    /// <summary>
    /// Translator indices: 0 = Google Translate, 1 = Groq, 2 = Gemini (the Gemini
    /// recognizer translates on its own), 3 = OpenAI, 4 = Claude, 5 = DeepSeek,
    /// 6 = Kimi (Moonshot) and 7 = GLM (Zhipu).
    /// </summary>
    private ITranslator? CreateTranslator(bool suppressMissingKeyErrors = false)
    {
        switch (AppConfig.ConfigObject.Translator)
        {
            case 0:
                return new GoogleTranslate();
            case 1:
                return CreateProviderTranslator(ApiProviders.Groq, suppressMissingKeyErrors);
            case 2:
                return new GeminiStub();
            case 3:
                return CreateProviderTranslator(ApiProviders.OpenAi, suppressMissingKeyErrors);
            case 4:
                return CreateProviderTranslator(ApiProviders.Claude, suppressMissingKeyErrors);
            case 5:
                return CreateProviderTranslator(ApiProviders.DeepSeek, suppressMissingKeyErrors);
            case 6:
                return CreateProviderTranslator(ApiProviders.Kimi, suppressMissingKeyErrors);
            case 7:
                return CreateProviderTranslator(ApiProviders.Glm, suppressMissingKeyErrors);
            default:
                return CreateCustomTranslator(suppressMissingKeyErrors);
        }
    }

    /// <summary>
    /// Translators at or above <see cref="ApiProviders.CustomIndexBase"/> are providers the user
    /// added himself. They are stored by id, so reordering the list keeps the selection.
    /// </summary>
    private ITranslator? CreateCustomTranslator(bool suppressMissingKeyErrors = false)
    {
        var index = AppConfig.ConfigObject.Translator;

        var custom = ApiProviders.FindCustom(AppConfig.ConfigObject, AppConfig.ConfigObject.CustomProviderId)
                     ?? ApiProviders.FindCustom(AppConfig.ConfigObject, (index - ApiProviders.CustomIndexBase).ToString());

        if (custom == null)
        {
            Log.Error($"[APP] The selected custom provider ({index}) does not exist (anymore)");
            if (!suppressMissingKeyErrors) _errorHandler.OnError("CUSTOM_PROVIDER_MISSING");

            return null;
        }

        return CreateProviderTranslator(custom, suppressMissingKeyErrors);
    }

    private ITranslator? CreateProviderTranslator(ApiProviderInfo provider, bool suppressMissingKeyErrors = false)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKey(AppConfig.ConfigObject)))
        {
            if (!suppressMissingKeyErrors)
            {
                Log.Error($"[{provider.Id.ToUpperInvariant()}] No API key is configured!");
                _errorHandler.OnError($"{provider.Id.ToUpperInvariant()}_NO_API_KEY");
            }

            return null;
        }

        if (!ValidateApiKey(provider)) return null;

        return provider.Kind switch
        {
            ApiProviderKind.Anthropic => new AnthropicTranslator(provider),
            ApiProviderKind.Google => new GeminiChatTranslator(provider),
            _ => new OpenAiChatTranslator(provider)
        };
    }

    /// <summary>
    /// Verifies the key against the provider's model list. A network problem does not
    /// block the start (the key might still be fine), only an explicit "unauthorized"
    /// answer from the provider does.
    /// </summary>
    private bool ValidateApiKey(ApiProviderInfo provider)
    {
        try
        {
            using var client = NetworkClientFactory.Create(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(HttpMethod.Get, provider.ModelsUrl(AppConfig.ConfigObject, provider.ApiKey(AppConfig.ConfigObject)));
            provider.ApplyAuth(request, provider.ApiKey(AppConfig.ConfigObject));

            using var response = client.Send(request);
            var status = (int) response.StatusCode;

            Log.Debug($"[{provider.Id.ToUpperInvariant()}] API key check: status={status}, model={provider.ResolveModel(AppConfig.ConfigObject)}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Error($"[{provider.Id.ToUpperInvariant()}] Invalid API key ({status})");
                _errorHandler.OnError($"{provider.Id.ToUpperInvariant()}_INVALID_API_KEY");

                return false;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.Warning($"[{provider.Id.ToUpperInvariant()}] The provider refused the model list request ({status}); continuing, but check the API key permissions");
            }

            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"[{provider.Id.ToUpperInvariant()}] Unable to verify the API key ({e.Message}); continuing anyway");

            return true;
        }
    }

    public void Stop()
    {
        Log.Information($"[APP] Stopping...");
        _running = false;

        _microphoneKikitan?.Dispose();
        _microphoneKikitan = null;
        _desktopKikitan?.Dispose();
        _desktopKikitan = null;

        ClearOverlay();
        _appState.Status = 0;
        SendUpdateToUI();
    }

    public async void RestartIfRunning()
    {
        Log.Information($"[APP] Restart requested: running={_running}");
        // Double checking _running is not really sensible but idc honestly, this works
        if (_running)
        {
            _appState.Status = 1;
            Stop();

            await Task.Delay(100);
            
            Log.Information($"[APP] Restart delay completed: running={_running}, willRestart={_running}");
            if (!_running) Start();
        }
        
        SendUpdateToUI();
    }

    public void ManualTranslate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_microphoneKikitan != null && _running)
        {
            _microphoneKikitan.ManualTranslate(text);
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var translator = _translator ?? CreateTranslator();
                var translated = AppConfig.ConfigObject.SpeechToTextOnly
                    ? ""
                    : translator?.Translate(text, AppConfig.ConfigObject.SourceLanguage, AppConfig.ConfigObject.TargetLanguage) ?? "";

                SendRecognitionData(text, translated, true);
                UpdateOverlaySpeaker(isOther: false, text, translated, true);

                if (AppConfig.ConfigObject.SendToChatbox)
                {
                    var chatbox = new Chatbox();
                    chatbox.Send(text, translated, true);
                }

                if (AppConfig.ConfigObject.SendUserData)
                {
                    var osc = new OSC("/microphone");
                    osc.Send(text, translated, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[APP] Manual translation error while engine stopped");
                _errorHandler.OnError($"Manual translation failed: {ex.Message}");
            }
        });
    }

    public void StartCloudUpdate()
    {
        if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) != 0) return;

        Task.Run(async () =>
        {
            try
            {
                var info = _pendingUpdate ?? await CloudUpdater.CheckForUpdateAsync(_appState.AppVersion);
                if (info == null)
                {
                    SendUpdateProgress(0, "", "未检测到可用的新版本或 gx.txt 配置无效。");
                    Interlocked.Exchange(ref _updateInProgress, 0);
                    return;
                }

                await CloudUpdater.DownloadAndApplyAsync(
                    info,
                    (pct, status, err) =>
                    {
                        SendUpdateProgress(pct, status, err);
                        if (!string.IsNullOrEmpty(err)) Interlocked.Exchange(ref _updateInProgress, 0);
                    },
                    onBeforeExit: () =>
                    {
                        try { Stop(); } catch { }
                        try { StopLeftoverOverlays(); } catch { }
                    });
            }
            catch (Exception ex)
            {
                SendUpdateProgress(0, "", $"更新失败：{ex.Message}");
                Interlocked.Exchange(ref _updateInProgress, 0);
            }
        });
    }

    private void SendUpdateProgress(int percent, string status, string? error)
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "update_progress",
                Data = JsonConvert.SerializeObject(new { percent, status, error })
            }));
    }

    public void SendUpdateToUI()
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "state",
                Data = JsonConvert.SerializeObject(_appState)
            }));
    }

    private void SendRecognitionData(string recognized, string translated, bool final)
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "recognition",
                Data = JsonConvert.SerializeObject(new RecognitionData { Transcription = recognized, Translation = translated, Final = final })
            }));
    }

    private void SendMicChanged()
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "mic_changed",
                Data = ""
            }));
    }

    private void UpdateOverlaySpeaker(bool isOther, string original, string translated, bool final)
    {
        if (_writer == null) return;
        var cfg = AppConfig.ConfigObject;
        if (cfg == null) return;

        string cleanOrig = original?.Trim() ?? string.Empty;
        string cleanTrans = cfg.SpeechToTextOnly ? string.Empty : (translated?.Trim() ?? string.Empty);
        if (cleanOrig.Length == 0 && cleanTrans.Length == 0) return;

        lock (_overlayStateLock)
        {
            if (!final)
            {
                var now = Environment.TickCount64;
                ref long lastTick = ref (isOther ? ref _lastOtherInterimOverlayWriteTick : ref _lastSelfInterimOverlayWriteTick);
                if (now - lastTick < 90) return;
                lastTick = now;
            }
        }

        _writer.PushEvent(
            isOther: isOther,
            original: cleanOrig,
            translation: cleanTrans,
            isFinal: final,
            desktopEnabled: cfg.DesktopOverlayEnabled,
            lightMode: cfg.LightMode,
            speechToTextOnly: cfg.SpeechToTextOnly,
            noLanguageSpace: IsNoSpaceLanguage(cfg),
            opacity: cfg.GlassOpacity,
            uiLangCode: GetUiLangCode(cfg.Language),
            position: cfg.OverlayPosition,
            scalePercent: cfg.OverlayScale);
    }

    public void PreviewOverlay()
    {
        if (_writer == null) return;
        var cfg = AppConfig.ConfigObject;
        if (cfg == null) return;

        bool noSpace = IsNoSpaceLanguage(cfg);
        int uiLang = GetUiLangCode(cfg.Language);
        _writer.ClearHistory();

        void SendDemo(bool isOther, string orig, string trans) =>
            _writer.PushEvent(isOther, orig, trans, true, true, cfg.LightMode, false, noSpace, cfg.GlassOpacity, uiLang, cfg.OverlayPosition, cfg.OverlayScale);

        SendDemo(true, "Hey there, welcome to this VRChat world!", "你好呀，欢迎来到这个 VRChat 世界！");
        SendDemo(false, "谢谢！这里的灯光效果和音乐真不错。", "Thanks! The lighting and music here are super nice.");
        SendDemo(true, "Want to take a group photo near the mirror?", "要不要一起去镜子旁边拍张合照？");
        SendDemo(false, "好呀，我马上过来！", "Sure, coming right over!");
    }

    public void ClearOverlay() => _writer?.ClearHistory();

    private void SyncOverlayTheme()
    {
        if (_writer == null) return;
        var cfg = AppConfig.ConfigObject;
        if (cfg == null) return;

        _writer.UpdateTheme(
            desktopEnabled: cfg.DesktopOverlayEnabled,
            lightMode: cfg.LightMode,
            speechToTextOnly: cfg.SpeechToTextOnly,
            noLanguageSpace: IsNoSpaceLanguage(cfg),
            opacity: cfg.GlassOpacity,
            uiLangCode: GetUiLangCode(cfg.Language),
            position: cfg.OverlayPosition,
            scalePercent: cfg.OverlayScale);
    }

    private static bool IsNoSpaceLanguage(ConfigObject cfg) =>
        cfg.SourceLanguage is "ja" or "ko" or "zh" or "cn" || cfg.TargetLanguage is "ja" or "ko" or "zh" or "cn";

    private static int GetUiLangCode(string? lang) => (lang ?? "").ToLowerInvariant() switch
    {
        "en" => 1,
        "jp" or "ja" => 2,
        "kr" or "ko" => 3,
        _ => 0,
    };
}