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
    [JsonProperty("app_version")] public string AppVersion = "Developer Build";
    [JsonProperty("server_version")] public string ServerVersion;
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
    private bool _running;

    private OverlayWriter? _writer;

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
        
        #if DEBUG
        _loopback = new("Resources/wwwroot/silero_vad.onnx");
        _mic = new("Resources/wwwroot/silero_vad.onnx", _errorHandler);
        #else
        _appState.AppVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split("+")[0];
        _loopback = new(Path.Combine(AppContext.BaseDirectory, "wwwroot", "silero_vad.onnx"));
        _mic = new(Path.Combine(AppContext.BaseDirectory, "wwwroot", "silero_vad.onnx"), _errorHandler);
        #endif
        
        _appState.IsLinux = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _appState.IsAppimage = _appState.IsLinux && Environment.GetEnvironmentVariable("KIKITAN_NOT_APPIMAGE") == null;
        _connector = connector;

        // The self update feed must point at your own repository: using the original project
        // would let the updater replace this build with the upstream (unmodified) app.
        if (string.IsNullOrWhiteSpace(Constants.UPDATE_FEED_URL))
        {
            Log.Information("[APP] Self update check is disabled: no update feed is configured");
        }
        else
        {
            Task.Run(async () =>
            {
                var mgr = new UpdateManager(new GithubSource(Constants.UPDATE_FEED_URL, null, true));

                var newVersion = await mgr.CheckForUpdatesAsync();
                _appState.ServerVersion = newVersion?.TargetFullRelease.Version.ToString();

                SendUpdateToUI();
            });
        }
        
        AppConfig.OnUpdate += () =>
        {
            _appState.Config = AppConfig.ConfigObject;

            SendUpdateToUI();
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !noUI)
        {
            StopLeftoverOverlays();

            try
            {
                _writer = new();
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

                if (_appState.Config.Microphone.Length != 0 && !mics.Exists(m => m.Name == _appState.Config.Microphone))
                {
                    var device = engine.CaptureDevices.FirstOrDefault(d => d.IsDefault);
                    if (device == null) device = engine.CaptureDevices[0];
                    
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

        IRecognizer rMic;
        if (AppConfig.ConfigObject.Recognizer == 0) rMic = new Bing(_mic);
        else if (AppConfig.ConfigObject.Recognizer == 1) rMic = new GroqRecognizer(_mic);
        else rMic = new Gemini(_mic);

        var translator = CreateTranslator();
        if (translator == null)
        {
            Log.Error($"[APP] Unable to start: the configured translator ({AppConfig.ConfigObject.Translator}) is not usable");

            return;
        }

        _translator = translator;

        _microphoneKikitan = new Kikitan(rMic, _translator, new ErrorHandler(_connector), false);
        _microphoneKikitan.AddOutput(new Custom(SendRecognitionData, false));
        if (AppConfig.ConfigObject.SendToChatbox)
        {
            var chatbox = new Chatbox();
            
            _microphoneKikitan.AddOutput(new Custom((r, t, f) =>
            {
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
            IRecognizer rDesktop;
            
            if (AppConfig.ConfigObject.Recognizer == 0) rDesktop = new Bing(_loopback);
            else if (AppConfig.ConfigObject.Recognizer == 1) rDesktop = new GroqRecognizer(_loopback);
            else rDesktop = new Gemini(_loopback);
            
            _desktopKikitan = new Kikitan(rDesktop, _translator, new ErrorHandler(_connector), true);
            Log.Information($"[APP] Desktop pipeline starting: recognizer={rDesktop.GetType().Name}, overlayAvailable={_writer != null}");
            if (_writer != null)
            {
                _desktopKikitan.AddOutput(new Custom((recognized, translated, final) =>
                {
                    var text = AppConfig.ConfigObject.SpeechToTextOnly ? recognized : translated;
                    var time = text.Length * AppConfig.ConfigObject.ChatboxWaitPerCharMs;

                    if (text.Trim().Length == 0) return;
                    Log.Debug($"[LOOP] Writing overlay output: chars={text.Length}, final={final}, durationMs={Math.Max(5000, time)}");
            
                    _writer.Write(new OverlayPipeData { Text = text, NoLanguageSpace =
                        (AppConfig.ConfigObject.SourceLanguage == "ja" || AppConfig.ConfigObject.SourceLanguage == "ko" ||
                         AppConfig.ConfigObject.SourceLanguage == "cn"), Time = time < 5000 ? 5000 : time});
                }, false));
            }
            
            if (AppConfig.ConfigObject.SendUserData)
            {
                _desktopKikitan.AddOutput(new OSC("/desktop"));
            }
            
            _desktopKikitan?.Start();
        }
        
        _running = true;
        Log.Information($"[APP] Startup complete.");
        SendUpdateToUI();
    }

    /// <summary>
    /// Translator indices: 0 = Google Translate, 1 = Groq, 2 = Gemini (the Gemini
    /// recognizer translates on its own), 3 = OpenAI, 4 = Claude, 5 = DeepSeek,
    /// 6 = Kimi (Moonshot) and 7 = GLM (Zhipu).
    /// </summary>
    private ITranslator? CreateTranslator()
    {
        switch (AppConfig.ConfigObject.Translator)
        {
            case 0:
                return new GoogleTranslate();
            case 1:
                return CreateProviderTranslator(ApiProviders.Groq);
            case 2:
                return new GeminiStub();
            case 3:
                return CreateProviderTranslator(ApiProviders.OpenAi);
            case 4:
                return CreateProviderTranslator(ApiProviders.Claude);
            case 5:
                return CreateProviderTranslator(ApiProviders.DeepSeek);
            case 6:
                return CreateProviderTranslator(ApiProviders.Kimi);
            case 7:
                return CreateProviderTranslator(ApiProviders.Glm);
            default:
                return CreateCustomTranslator();
        }
    }

    /// <summary>
    /// Translators at or above <see cref="ApiProviders.CustomIndexBase"/> are providers the user
    /// added himself. They are stored by id, so reordering the list keeps the selection.
    /// </summary>
    private ITranslator? CreateCustomTranslator()
    {
        var index = AppConfig.ConfigObject.Translator;

        var custom = ApiProviders.FindCustom(AppConfig.ConfigObject, AppConfig.ConfigObject.CustomProviderId)
                     ?? ApiProviders.FindCustom(AppConfig.ConfigObject, (index - ApiProviders.CustomIndexBase).ToString());

        if (custom == null)
        {
            Log.Error($"[APP] The selected custom provider ({index}) does not exist (anymore)");
            _errorHandler.OnError("CUSTOM_PROVIDER_MISSING");

            return null;
        }

        return CreateProviderTranslator(custom);
    }

    private ITranslator? CreateProviderTranslator(ApiProviderInfo provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKey(AppConfig.ConfigObject)))
        {
            Log.Error($"[{provider.Id.ToUpperInvariant()}] No API key is configured!");
            _errorHandler.OnError($"{provider.Id.ToUpperInvariant()}_NO_API_KEY");

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
        _microphoneKikitan?.Dispose();
        _desktopKikitan?.Dispose();

        _running = false;
        
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
        
}