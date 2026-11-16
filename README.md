<div align="center">

<img src="MsgOptimizer/src/app.png" width="72" alt="Msg Optimizer logo">

# Msg Optimizer

**Rewrite any text, in any Windows app, straight from the right-click menu.**

Select text in Teams, Word, Edge, Notepad and more → right-click → **Optimize Text** → done.

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4.svg)](#-quick-start)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![Language: C#](https://img.shields.io/badge/language-C%23-239120.svg)](MsgOptimizer/src)
[![Dependencies: none](https://img.shields.io/badge/dependencies-none-brightgreen.svg)](#-quick-start)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-ff69b4.svg)](CONTRIBUTING.md)
<!-- After publishing, add the build badge (replace OWNER/REPO):
[![Build](https://github.com/OWNER/REPO/actions/workflows/build.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/build.yml)
-->

[Features](#-features) · [Quick start](#-quick-start) · [Usage](#-usage) · [How it works](#-how-it-works) · [Privacy](#-privacy--security) · [Contributing](#-contributing)

</div>

<!--
  📸 DEMO: drop a GIF or screenshot here, e.g. docs/images/demo.gif, then uncomment:
  <p align="center"><img src="docs/images/demo.gif" width="720" alt="Selecting text in Teams and choosing Optimize Text → Enhance for Teams"></p>
-->

---

## 💡 Why

Polishing a message today means: copy it → open a chatbot → paste → write a prompt → copy the answer → go back → paste over the original. Every time.

Msg Optimizer turns that into **one right-click**, right where you're writing. It adds an **Optimize Text** entry next to the app's own Cut / Copy / Paste, rewrites the selection with the AI model of your choice, and puts the result back in place.

```text
✎ Optimize Text  ›   ✉  Enhance for Mail
                     💬 Enhance for Teams
                     ✓  Correct English
                     👤 Humanize
┌──────────────────────────┐
│ Cut          Ctrl+X      │   ← the app's own menu, untouched
│ Copy         Ctrl+C      │
│ Paste        Ctrl+V      │
└──────────────────────────┘
```

## ✨ Features

- **Works across apps.** Tested in Microsoft Teams, Edge, Word and Notepad, and built for any app with selectable text, including Chromium/Electron/WebView2 apps such as Chrome and WhatsApp Desktop.
- **Feels native.** The item docks onto the app's own context menu and matches its width and colours, dark menus included. It never replaces that menu.
- **Four one-click rewrites:** *Enhance for Mail*, *Enhance for Teams*, *Correct English* and *Humanize*. Add your own actions and prompts in one JSON file.
- **Bring your own model:** Google Gemini, Groq, OpenRouter, a local Ollama, or any OpenAI-compatible server.
- **Safe by design.**
  - `Ctrl+Z` undoes a replacement.
  - Your clipboard is restored once the replacement is confirmed.
  - If it can't be confirmed (read-only text, or you clicked or typed elsewhere while the model was working), nothing is pasted and the result is left on the clipboard instead, so nothing is lost.
- **Private.** No telemetry and no server of its own. The API key is encrypted with Windows DPAPI, and the log never contains your text.
- **Tiny and dependency-free.** A single ~90 KB `.exe`, built with the C# compiler that already ships with Windows. No installer, SDK or runtime to download.

## 🚀 Quick start

**Requirements:** Windows 11, or Windows 10 version 1903 or later. Both include .NET Framework 4.8; older Windows 10 needs the [.NET Framework 4.8 runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48). Nothing else.

**1. Get the code:** download the ZIP from GitHub, or clone it:

```bat
git clone https://github.com/OWNER/REPO.git
cd REPO
```

**2. Build** (compiles and runs the self-test in a few seconds):

```bat
MsgOptimizer\build.cmd
```

```text
Built bin\MsgOptimizer.exe
```

**3. Run it:**

```bat
MsgOptimizer\bin\MsgOptimizer.exe
```

A green ✦ icon appears in the notification area. Click the **^** arrow if it's hidden.

**4. Connect an AI model.** Double-click the tray icon, or run `MsgOptimizer.exe --settings`, then:

1. Pick a **Provider** and click **Get a key**.
2. Paste the key, click **Test connection**, then **Save**.

> 💡 No key yet? The app starts in **demo mode**: it replaces the selection with `[Correct English] your text`, so you can try the whole flow without an account.

**5. (Optional) Start with Windows:** right-click the tray icon and tick **Start with Windows**.

## 🧭 Usage

1. Select text anywhere: a chat box, an email draft, a document, a web form.
2. Right-click the selection.
3. Hover **Optimize Text** and choose an action.
4. The text is replaced. Press `Ctrl+Z` in the app if you want the original back.

### What the actions do

| Action | Before | After *(illustrative; the wording depends on your model)* |
| --- | --- | --- |
| **Enhance for Mail** | `hey can u send me the report by tomorow, its urgent` | `Hi, could you please send me the report by tomorrow? It's urgent.` |
| **Enhance for Teams** | `i wanted to check if anyone had time to maybe look at the PR i raised yesterday` | `Could someone review my PR from yesterday when you get a chance?` |
| **Correct English** | `i has finished the task and want feedback on it` | `I have finished the task and would like feedback on it.` |
| **Humanize** | `I hope this message finds you well. Furthermore, I wish to delve into the matter.` | `Hope you're doing well. I'd like to dig into this a bit more.` |

### Add your own action

Settings → **Edit menu items and prompts…** opens `%APPDATA%\MsgOptimizer\settings.json`. Every entry in `Actions` becomes a submenu item:

```json
{
  "Label": "Make it shorter",
  "Icon": "E70F",
  "Prompt": "Rewrite the draft in at most two sentences. Keep every fact, name and number."
}
```

Changes apply on the next right-click. Output rules are appended to every prompt automatically: return only the rewrite, treat the selection as data, and keep names, numbers, links and @mentions.

### Providers

| Provider | Server URL | Example model | Key |
| --- | --- | --- | --- |
| Google Gemini *(default, free tier)* | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-flash-latest` | [Get a key](https://aistudio.google.com/apikey) |
| Groq *(free tier, very fast)* | `https://api.groq.com/openai/v1` | `llama-3.3-70b-versatile` | [Get a key](https://console.groq.com/keys) |
| Ollama *(runs on your PC)* | `http://localhost:11434/v1` | `llama3.2:3b` | none |
| OpenRouter, or any OpenAI-compatible server | its `/v1` URL | any it lists | its site |

### Command line

| Command | What it does |
| --- | --- |
| `MsgOptimizer.exe` | Start the app (one instance per user) |
| `MsgOptimizer.exe --settings` | Open only the Settings window |
| `MsgOptimizer.exe --selftest` | Run the built-in logic checks (exit code 0 = pass) |
| `MsgOptimizer.exe --selftest --ping` | Also send one real request to the configured provider |

## 🧩 How it works

No Windows API lets another program add an item *inside* the menus that Chromium, WebView2, WinUI or Office apps draw themselves. So Msg Optimizer draws its own row and docks it flush against that menu:

```text
 right-click ──► global mouse hook (own thread, never blocks the click)
                   │
                   ├─► UI Automation: is text selected?   (Chromium, WebView2, Word, Notepad…)
                   ├─► find the app's menu window → dock our row onto it, match width + colours
                   │
 click "Correct English"
                   │
                   ├─► Esc (close app menu) → Ctrl+C → OpenAI-compatible API → Ctrl+V
                   └─► UI Automation confirms the replacement → restore your clipboard
```

| Component | Responsibility |
| --- | --- |
| `InputHook` | Low-level mouse and keyboard hooks on a dedicated thread; swallows clicks on our row only |
| `MenuController` | Decides when to show the row, finds and docks to the app's menu, handles hover and clicks |
| `MenuWindow` | The never-focused menu window: drawing, icons, colours sampled from the app's menu |
| `TextReplacer` | Copy → rewrite → paste → verify, with clipboard safety |
| `LlmClient` | One OpenAI-compatible request plus output cleanup |
| `Settings` / `SettingsForm` | `settings.json`, the DPAPI-encrypted key, the Settings window |

📖 Details, design decisions, the test record and known limitations are in **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

## 🔒 Privacy & security

- **Your text goes only to the provider you configure**, and only when you click an action.
- **No telemetry, no accounts, no server** run by this project.
- **The keyboard hook doesn't record what you type.** It reacts to key presses only to close the menu and to notice selection shortcuts such as Ctrl+A, and it never stores or sends them.
- **The API key is encrypted** with Windows DPAPI, readable only by your Windows account on your PC.
- **The diagnostics log** (`%APPDATA%\MsgOptimizer\msgoptimizer.log`) records decisions, window classes and text *lengths*, never the text itself.

Found a vulnerability? Please report it privately; see [SECURITY.md](SECURITY.md).

## ❓ FAQ

<details>
<summary><b>I can't find the tray icon.</b></summary>

Windows hides new icons. Click the **^** arrow at the right end of the taskbar and drag the ✦ icon onto the taskbar. You can always open settings with `MsgOptimizer.exe --settings`.
</details>

<details>
<summary><b>Windows SmartScreen or my antivirus warns about the .exe.</b></summary>

The executable is unsigned, and it uses global input hooks and simulated keystrokes, which is what security tools look for. Building it yourself from this source with `build.cmd` (about 1,900 readable lines of C#) is the best way to trust it.
</details>

<details>
<summary><b>"Optimize Text" doesn't appear in some app.</b></summary>

- Make sure text is selected.
- Apps running **as administrator** can't be reached by a normal process; run Msg Optimizer as administrator too.
- Otherwise, open the tray menu → **Open log** and look at the `Right-click in …` line. Include it in a [bug report](.github/ISSUE_TEMPLATE/bug_report.md).

</details>

<details>
<summary><b>Can it work offline?</b></summary>

Yes. Run a model locally with [Ollama](https://ollama.com) and choose **Ollama** as the provider. Nothing leaves your PC.
</details>

## 📁 Project structure

```text
.
├── MsgOptimizer/            # The Windows app (C#, .NET Framework 4.8)
│   ├── build.cmd            # Build + self-test with Windows' own compiler
│   └── src/                 # 14 small source files, one responsibility each
├── docs/ARCHITECTURE.md     # Design decisions, test record, limitations
└── .github/                 # Issue and PR templates, CI workflow
```

## 🤝 Contributing

Contributions are welcome: bug reports with the log line, fixes, new prompt ideas, or testing in apps not listed yet. Read **[CONTRIBUTING.md](CONTRIBUTING.md)** to set up in a few minutes (no SDK required), and follow the **[Code of Conduct](CODE_OF_CONDUCT.md)**.

## 🙏 Acknowledgments

- Microsoft **UI Automation** and the Win32 input APIs, which make cross-app text access possible.
- The **Segoe Fluent Icons / Segoe MDL2 Assets** fonts that ship with Windows, used for menu icons (not redistributed).
- **Google Gemini, Groq, Ollama and OpenRouter** for OpenAI-compatible APIs, so one client works with all of them.
- **[Contributor Covenant](https://www.contributor-covenant.org)** for the Code of Conduct, and **[shields.io](https://shields.io)** for the badges.

## 📄 License

[MIT](LICENSE) © 2026 Suraj Kumar Yadav
