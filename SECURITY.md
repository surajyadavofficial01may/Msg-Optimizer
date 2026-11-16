# Security Policy

Msg Optimizer installs global mouse and keyboard hooks, reads selected text, and talks to an AI provider on your behalf. We take that responsibility seriously and appreciate reports that help keep users safe.

## Supported versions

| Version | Supported |
| --- | --- |
| 2.x (latest) | ✅ |
| < 2.0 | ❌ |

Security fixes go into the latest release only.

## Reporting a vulnerability

**Please don't report security problems in public issues, discussions or pull requests.**

Report privately through GitHub instead: open this repository's **Security** tab, choose **Report a vulnerability**, and fill in the form. Only the maintainers can see it.

Please include:

- **What the issue is** and what an attacker could achieve.
- **Steps to reproduce:** the Windows version, the app the text was selected in, and the provider, if relevant.
- **The Msg Optimizer version** (right-click `MsgOptimizer.exe` → Properties → Details).
- **Any proof of concept**, logs or screenshots. Remove personal text and API keys first.

### What happens next

1. We'll acknowledge your report as soon as we can. This is a volunteer-maintained project, so please allow a few days.
2. We'll confirm the problem, decide on a fix, and keep you updated on progress.
3. Once a fix is released, we'll publish an advisory and credit you, unless you'd rather stay anonymous.

## Security model

Knowing what the app is supposed to do helps you spot when it doesn't.

| Area | Intended behaviour |
| --- | --- |
| **Keyboard hook** | Reacts to key presses only to close its menu, to notice selection shortcuts (Shift+arrows, Ctrl+A), and to detect that you moved on during a rewrite. It never stores, logs or sends keystrokes. |
| **Mouse hook** | Swallows clicks only on Msg Optimizer's own menu row; all other input passes through unchanged. |
| **Selected text** | Read via UI Automation and the clipboard only when you right-click with a selection. It's sent only to the provider you configured, and only when you click an action. |
| **Clipboard** | Backed up before copying and restored after a confirmed replacement. When the replacement can't be confirmed, the rewrite is left on the clipboard so it isn't lost. |
| **API key** | Stored in `%APPDATA%\MsgOptimizer\settings.json`, encrypted with Windows DPAPI (current user). A plain-text `ApiKey` is honoured only if you type one in by hand. |
| **Network** | Requests go only to the server URL you configure (HTTPS with TLS 1.2 for every built-in provider; plain HTTP is meant for local servers such as Ollama). There is no telemetry and no other endpoint. |
| **Logs** | `%APPDATA%\MsgOptimizer\msgoptimizer.log` records decisions, window classes and text *lengths*, never text content or keys. |

**In scope:**

- Keystrokes or selected text being stored, logged or sent anywhere unexpected.
- API key disclosure.
- Input or text ending up in the wrong window.
- Clipboard data loss.
- Anything that lets another process or a malicious web page abuse the hooks.

**Out of scope:**

- How your chosen AI provider handles data you send it.
- Issues that need an attacker to already control your Windows account.
- Antivirus or SmartScreen warnings about the unsigned executable (see the README FAQ).
