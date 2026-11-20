# Kikitan Translator

<div align="center">
<a href="https://sergiomarquina.booth.pm/items/6073050">
<img src="https://media.buyee.jp/guide/addtobuyee/assets/img/store-logo-booth.png" alt="drawing" width="150" align="center">
</a>
<a href="https://buymeacoffee.com/sergiomarquina">
<img src="https://i.imgur.com/l7NBjqk.png" alt="drawing" width="150" height="45" align="center">
</a>
<br><br>
<img width=500 src="https://i.imgur.com/C9fSR9O.png" />
</div>

### A VRChat translator built for helping you and the person/people you're talking with to understand each other regardless of language differences.

- **Translation:** Translation of your speech to the chatbox in more than 10 major languages (6 accents of English and 6 dialects of Spanish along with languages such as Japanese, Korean, Chinese, Italian, French, Turkish, Russian, Polish, Portugal, German, French, Arabic, Swedish and so on)
- **Transcription (Just Speech to Text):** If you don't want to translate, there is a transcription mode that sends whatever you say directly to the chatbox. Perfect for people that prefer to not speak in VRChat but be able to communicate with the convenience of speaking.

## Translation providers

Kikitan can translate with the following services (Settings -> Translation Service):

| Service | Notes |
| --- | --- |
| Google Translate | Free, no API key required |
| Groq | OpenAI compatible chat completions, free tier available |
| Gemini | Used together with the Gemini speech recognition, which translates with its own live model |
| OpenAI | Needs an OpenAI API key |
| Claude | Anthropic messages API |
| DeepSeek | OpenAI compatible endpoint |
| Kimi | Moonshot / Kimi chat completions |
| GLM | Zhipu BigModel open platform |

Enter the API key of a provider in **Settings -> Translation API**. As soon as a key has been
entered, the available model list is requested from the provider automatically and can be picked
in the model field of that provider (an empty model field uses the provider default). Every
provider also accepts a custom base URL, which is useful for relay or mirror endpoints.

### Custom providers

Any OpenAI, Claude or Gemini compatible endpoint (relay services, self hosted gateways, ...) can be
added under **Settings -> Translation API -> Custom providers**: give it a name, pick the request
style (OpenAI / Claude / Gemini), enter its base URL (`https://xxx.xxx/v1`) and an API key. As soon
as the key is entered the model list is fetched automatically, and **Detect protocol** tries all
three request styles and keeps the one that answers. Custom providers show up in the translation
service list and behave like the built in ones - including thinking being switched off.

### Thinking / reasoning is disabled by default

Translation is a latency sensitive task, so Kikitan sends every request with the model's
thinking (reasoning) mode switched off where the provider supports it: DeepSeek gets
`thinking: {"type": "disabled"}` (its default is *on*), Kimi and GLM the same, Groq and OpenAI get
`reasoning_effort: "none"` and Claude gets `thinking: {"type": "disabled"}`. Models that force
thinking (for example GLM-5.3) or providers that reject the parameter keep working - the parameter
is dropped automatically after the first rejection. It can be switched off entirely on the
"Translation API" settings page.

## Proxy support

**Settings -> Proxy** lets you add any number of HTTP, HTTPS, SOCKS5 or SOCKS4(a) proxies, each
with an optional username/password. One proxy can be selected at a time and can be tested against
a small endpoint before it is used. The selected proxy is used for every outgoing API request:
translation, speech recognition and model list requests. Without a selection Kikitan connects
directly.

The settings are stored in `%APPDATA%\Kikitan Translator\config.json` (`config.json` inside the
application data folder on Linux).

## How to build

### Required dependencies

- rust (recommended minimum is 1.77.2)
- node (recommended minimum is v22.1.0)

```sh
git clone https://github.com/YusufOzmen01/kikitan-translator
cd kikitan-translator

# to update dependencies
npm i

# for development
npm run tauri dev

# signing is required if you want to use the updater
# if don't want to use the updater, delete the updater plugin inside of tauri.conf.json

# to sign the executable, generate a private and public keypair, then assign TAURI_SIGNING_PRIVATE_KEY environment variable and update the pubkey field inside of tauri.conf.json with your public key

# for compiling as a release build
npm run tauri build
```

## License

[Check the LICENSE.md for details](https://github.com/YusufOzmen01/kikitan-translator/blob/main/LICENSE.md)
