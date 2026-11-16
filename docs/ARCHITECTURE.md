# Msg Optimizer: architecture

This document explains how Msg Optimizer works, why it is built this way, what has been tested, and where its limits are. For installation and usage, see the [README](../README.md).

## 1. The problem

The goal: select text in *any* Windows app, right-click, and get an **Optimize Text** submenu next to the app's own Cut / Copy / Paste.

**Why this is hard:** there is no Windows API that lets one program add an item *inside* another program's context menu.

- **Shell verbs** (`HKEY_CLASSES_ROOT\…\shell`) only apply to files and folders in Explorer.
- **Chromium, Electron and WebView2 apps** draw their own menus. That covers Chrome, Edge, Teams and WhatsApp Desktop.
- **WinUI** (Windows 11 Notepad) and **Office** (Word) also draw their own menus.
- **Browser extensions** can add items, but only inside the browser.

## 2. The approach: a docked companion row

Msg Optimizer watches right-clicks globally. When text is selected, it shows its own small, never-focused window and **docks it flush against the app's menu**, matching that menu's width and background colour, so it reads as part of the same menu.

```text
 right-click ──► InputHook (own thread)          the right-click always passes through,
                   │                              so the app's own menu opens as usual
                   ▼
              MenuController
                   ├─ Selection.ProbeAsync ── UI Automation: is text selected? (400 ms budget)
                   ├─ FindHostMenu ────────── the app's menu window, anchored at the click
                   └─ Dock + MenuWindow ───── our row, same width/colours, never covering the menu
                   │
 click an action   ▼
              TextReplacer
                   ├─ Esc ─────────────────── close the app's menu (we swallowed the click)
                   ├─ Ctrl+C ──────────────── copy the selection (clipboard backed up first)
                   ├─ LlmClient ───────────── one OpenAI-compatible /chat/completions request
                   ├─ Ctrl+V ──────────────── paste the result
                   └─ verify ──────────────── UI Automation: did the selection change?
                                              yes → restore clipboard; no → leave result on it
```

### Step by step

| Step | How |
| --- | --- |
| **See every right-click** | Low-level mouse and keyboard hooks (`SetWindowsHookEx(WH_MOUSE_LL / WH_KEYBOARD_LL)`) on a dedicated thread that does nothing else. Windows silently removes a low-level hook whose thread is slow to answer, so the callback never blocks: it decides synchronously whether a click belongs to our row and posts everything else to the UI thread. |
| **Is text selected?** | UI Automation `TextPattern.GetSelection()`, starting from the element under the cursor and then the focused element. The nearest text pattern up the tree decides. If an app hides its text from UI Automation, a *selection hint* fills in: a drag, double-click, Shift+click, Shift+arrows or Ctrl+A in that window. |
| **Find the app's menu** | A popup window at least 100 × 60 px (so tooltips don't count) with a side at the click point. Its top or bottom must also be at the click, or it must span the click when the app shifted it to fit on screen. Word puts a search box above its first item, so the vertical tolerance is larger. The wait is 300 ms. |
| **Dock** | On top of the app's menu when it opened downwards (under it when it opened upwards), aligned with its left (or right) edge, and as wide as its visible area. If that spot is off-screen or would cover one of the app's own popups (Word's formatting toolbar), the row goes beside the menu instead. |
| **Match the look** | The app menu's background is sampled just inside its corner; two of three samples must agree, because one can hit an icon or the anti-aliased edge. Sampling repeats for 400 ms because a just-opened window may not have painted yet. Hover, border and text colours are derived from that background. |
| **Interact** | While the app's menu is open it holds mouse capture, so our window would never receive clicks. The hook hit-tests our row itself, drives hover and the submenu, and **swallows** clicks on it; everything else passes through. Our row closes when the app's menu closes, on any key, wheel or click elsewhere. |
| **Replace** | Esc closes the app's menu (a second Esc only if a native submenu kept it open). Then Ctrl+C, where the clipboard *sequence number* confirms the copy instead of a fixed sleep, then the AI request, then Ctrl+V. |
| **Verify** | 400 ms after pasting, UI Automation re-reads the selection. If it collapsed, the paste landed and the user's clipboard is restored. If it's unchanged, the app refused the paste and the result stays on the clipboard. If it's unknown, the result also stays. |

## 3. Design decisions

| Decision | Why |
| --- | --- |
| **C# on .NET Framework 4.8, compiled by the in-box `csc.exe`** | .NET Framework 4.8 ships with Windows 10 (1903+) and 11, and it includes UI Automation, WinForms and HttpClient. Users and contributors install nothing. The trade-off is C# 5 syntax (see [CONTRIBUTING.md](../CONTRIBUTING.md)). |
| **A docked row instead of a replacement menu** | Replacing the app's menu would hide its own items, such as spelling suggestions, Copilot actions and Paste options. Docking keeps them all. |
| **Hooks on their own thread** | A busy UI thread (clipboard, settings dialog) can never delay system-wide mouse input or get the hook removed by Windows. |
| **Swallow clicks on our row; pass right-clicks through** | Our row has to work while the app's menu holds mouse capture, and the app's menu must keep working. |
| **Esc + Ctrl+C / Ctrl+V instead of writing text through UI Automation** | Paste goes through the app's own editing pipeline, so undo, formatting rules and the rich-text editors inside web and Electron apps behave as if the user pasted. |
| **Restore the clipboard only after a confirmed replacement** | If a paste silently fails, restoring the old clipboard would lose the result. Keeping the result is the safe default. |
| **One OpenAI-compatible client** | Gemini, Groq, Ollama, OpenRouter and OpenAI all accept `/chat/completions`, so one ~150-line client covers them. |
| **Per-monitor DPI awareness (v2)** | Hook coordinates are physical pixels; without it, placement and hit-testing drift on scaled displays. |
| **TLS 1.2 set explicitly** | An assembly compiled without a target-framework attribute defaults to older TLS on .NET Framework, and HTTPS calls fail. |
| **Our own keystrokes are tagged (`dwExtraInfo`)** | The keyboard hook ignores the Esc / Ctrl+C / Ctrl+V we send, without ignoring *all* injected input. Remote-desktop tools and AutoHotkey still work. |

## 4. Code layout

| File | Responsibility |
| --- | --- |
| `Program.cs` | Entry point: DPI awareness, TLS 1.2, single instance, `--selftest`, `--settings` |
| `InputHook.cs` | Global hooks on their own thread; swallows clicks on our row; counts real user input |
| `MenuController.cs` | When to show the row, selection hints, finding and docking to the app's menu, hover/click, dismissal |
| `MenuWindow.cs` | The never-activated menu window: drawing, icon font, colours derived from the app's menu |
| `TextReplacer.cs` | Esc → copy → rewrite → paste → verify; clipboard policy; the "you moved on" check |
| `Selection.cs` | UI Automation selection probe; clipboard copy, backup and restore |
| `LlmClient.cs` | The OpenAI-compatible request, error messages, output cleanup |
| `Settings.cs` | `settings.json` load/save/validate, default prompts, DPAPI-encrypted API key |
| `SettingsForm.cs` | The Settings window (provider presets, key, model, Test connection) |
| `Tray.cs` · `Toast.cs` · `Log.cs` | Tray icon and menu; click-through status messages; diagnostics log |
| `SelfTest.cs` | Logic checks run by `build.cmd`; placement cases come from real logged geometry |
| `Native.cs` | Win32 interop |

## 5. Configuration reference

`%APPDATA%\MsgOptimizer\settings.json` is created on first run and re-read whenever it changes:

| Key | Default | Meaning |
| --- | --- | --- |
| `BaseUrl` | Gemini's OpenAI-compatible URL | Any server that implements `/chat/completions` |
| `Model` | `gemini-flash-latest` | Model id as the provider names it |
| `ApiKey` | `""` | Plain-text key, for hand-editing (optional) |
| `ApiKeyProtected` | `""` | DPAPI-encrypted key written by the Settings window |
| `Temperature` | `0.4` | Sampling temperature |
| `TimeoutSeconds` | `60` | Request timeout |
| `MaxTokens` | `0` | `max_tokens` limit; `0` = provider default. Useful for local models that ramble. |
| `Actions[]` | 4 actions | Submenu items: `Label`, `Icon` (Segoe icon code point, e.g. `E715`), `Prompt` |

**Demo mode:** with no key and a non-local server, actions return `[Label] original text` instead of calling an API, so the capture-and-replace pipeline can be tried without an account.

**Timing knobs** live at the top of `MenuController.cs` (`ProbeTimeoutMs`, `HostMenuWaitMs`) and `TextReplacer.cs` (`MenuCloseMs`, `PasteSettleMs`).

## 6. Test record

`build.cmd` runs `MsgOptimizer.exe --selftest` on every build. It covers output cleanup, settings parsing and round-trip, DPAPI encryption, colour sampling, docking/placement and hit-testing. The placement cases are real window geometry captured in the diagnostics log.

Version 2.0 was also tested live on Windows 11 at 125% scaling, with real mouse input and screenshots:

| Scenario | Result |
| --- | --- |
| Notepad (Windows 11) | Docks on top of Notepad's menu at the same width; submenu opens up and to the right; text replaced; clipboard restored |
| Edge, text box | Docks on Edge's dark menu with matching colours; replaced |
| Edge, rich-text box near the bottom of the screen | Edge shifts its menu up; the row docks above it, not over it; replaced |
| Edge, read-only page text | "This app didn't accept the paste"; result on the clipboard |
| Word | Docks beside Word's menu (its formatting toolbar is above); search box and toolbar stay visible; near the screen edge the submenu opens under the row; replaced |
| Microsoft Teams | Teams' menu is a WebView2 popup, found in 16–41 ms; docked; replacement confirmed (tested by the maintainer) |
| Nothing selected | Only the app's own menu appears |
| Clicked elsewhere while the model was working | Nothing pasted; "You moved on…"; result on the clipboard |
| Real AI requests | Google Gemini (`gemini-3.5-flash-lite`): real rewrites in about 2 s each; also verified against a local Ollama server |
| Settings window | Layout at 125%; Test connection shows the server's reply |

### Open items

- **WhatsApp Desktop:** reported working, but not yet confirmed in the logs. It uses the same WebView2 engine as Teams.
- **Word:** 1 of 8 full runs ended with Word's own "Word has encountered a problem" box, and the paste was refused. That run came straight after an interrupted test; the 7 runs since, including 5 in a row, were clean.
- **Providers:** Gemini works with a real key (`gemini-3.5-flash-lite`). The default alias `gemini-flash-latest`, Groq and OpenRouter haven't been tried with a key yet. The app-by-app tests above ran in demo mode.

## 7. Known limitations

- **It's a docked row, not a true menu item.** Keyboard navigation of the app's menu doesn't reach it.
- **Placement isn't guaranteed.** If an app's menu takes longer than 300 ms to open, or opens in an unusual spot, the row docks to the click point and could overlap it (the log then says `app menu none`). In a cramped screen corner, placement falls back to the nearest on-screen spot.
- **Apps running as administrator** are off-limits to a normal process (UIPI). Run Msg Optimizer as administrator too if needed.
- **Menus drawn inside the app's window** (some web apps) have no window to dock to; the row sits just above the click, where such menus usually don't reach.
- **The first right-click in a Chromium app** turns on its accessibility tree, which costs some memory, and the first check may time out. Selection hints still work.
- **Clipboard history and formatting:** the temporary copy and the result appear in Win+V history, and a replaced selection becomes plain text.
- **Excluded windows:** consoles and terminals (Ctrl+C would interrupt the running program), the desktop, the taskbar and File Explorer.
