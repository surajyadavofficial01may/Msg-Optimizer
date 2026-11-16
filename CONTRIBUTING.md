# Contributing to Msg Optimizer

Thanks for your interest! Bug reports, fixes, new prompt ideas and testing in apps that aren't listed yet are all welcome. By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report a bug.** Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md) and include the `Right-click in …` line from the log (tray → **Open log**). It shows what the app detected and never contains your text.
- **Test an app.** Tell us whether *Optimize Text* docks correctly in Outlook, Slack, Discord, Visual Studio Code, LibreOffice and so on.
- **Suggest a feature or a prompt.** Use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).
- **Send a pull request.** Read the rest of this guide first.

> 🔒 **Security issues:** please don't open a public issue. See [SECURITY.md](SECURITY.md).

## Development setup

You need **Windows 10 (1903+) or 11**. There is no SDK, NuGet package or Visual Studio to install, because the app builds with the C# compiler that ships with .NET Framework 4.8.

```bat
:: 1. Fork the repository on GitHub, then clone your fork
git clone https://github.com/<your-username>/<repo>.git
cd <repo>

:: 2. Build and run the self-test
MsgOptimizer\build.cmd

:: 3. Run your build (exit any installed copy first: only one instance runs at a time)
MsgOptimizer\bin\MsgOptimizer.exe
```

Any editor works. VS Code with the C# extension gives syntax highlighting; full IntelliSense would need a project file, which isn't part of the repo.

**Useful while developing:**

| Command / file | Purpose |
| --- | --- |
| `MsgOptimizer.exe --selftest` | Logic checks: cleanup, settings, encryption, placement, hit-testing |
| `MsgOptimizer.exe --selftest --ping` | Also sends one real request to the configured provider |
| `MsgOptimizer.exe --settings` | Opens only the Settings window |
| `%APPDATA%\MsgOptimizer\msgoptimizer.log` | What was detected on each right-click, and where the menu was placed |
| `%APPDATA%\MsgOptimizer\settings.json` | Provider, model and the menu's actions and prompts |

## Coding guidelines

### C# 5 only

The in-box compiler supports **C# 5**, so these newer features **won't compile**:

| Not available | Write instead |
| --- | --- |
| `$"Hello {name}"` | `"Hello " + name` |
| `obj?.Member`, `a ?? throw …` | explicit `null` checks |
| `int Width => 10;` | `int Width { get { return 10; } }` |
| `nameof(x)`, `out var x`, tuples, `catch … when` | the older equivalents |
| auto-property initializers (`{ get; set; } = 1`) | set the value in the constructor |

`async` / `await`, LINQ, lambdas and optional parameters are fine.

### Principles

- **One responsibility per file.** See the layout in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#4-code-layout). P/Invoke goes in `Native.cs`.
- **No new dependencies.** The app must keep building with nothing installed.
- **Never block the hook thread.** `InputHook` callbacks must return immediately; post work to the UI thread.
- **Never log or send the user's text.** Logs may contain lengths, window classes and decisions, never content.
- **Don't lose user data.** If a change touches the clipboard or pasting, the result must never be lost: leave it on the clipboard when in doubt.
- **Comment the *why*.** Explain non-obvious Windows behaviour next to the code that handles it.
- **Formatting:** 4 spaces in C#, 2 in JSON/YAML/Markdown; `.editorconfig` sets this up in most editors.

## Testing your change

1. **`build.cmd` must pass.** It runs the self-test.
2. **Add a self-test check** (`src/SelfTest.cs`) for any new pure logic, such as placement, parsing or cleanup. For placement, use geometry from a real log line where you can.
3. **Test by hand** in the apps your change affects. The baseline:

   | Scenario | Expected |
   | --- | --- |
   | Notepad: select, right-click, Correct English | Row docked on top of Notepad's menu; text replaced; `Ctrl+Z` restores it |
   | Edge: text box and read-only page text | Replaced; read-only gives "didn't accept the paste" + clipboard |
   | Word | Row docked beside Word's menu; formatting toolbar uncovered; replaced |
   | Right-click with nothing selected | Only the app's own menu |
   | Click elsewhere while the model works | Nothing pasted; result on the clipboard |

   Demo mode (no API key) is enough for UI changes; it tags the text instead of rewriting it.

## Submitting a pull request

1. **Branch** from `main`, with a short descriptive name (`fix/word-search-box-overlap`, `feat/shorten-action`).
2. **Commit** in small, focused steps with clear messages. [Conventional Commits](https://www.conventionalcommits.org) (`fix:`, `feat:`, `docs:`) are appreciated but not required.
3. **Update the docs** (`README.md`, `docs/ARCHITECTURE.md`) if behaviour or setup changes.
4. **Open the pull request** and fill in the template: what changed, why, and how you tested it, including which apps.
5. **Reviews** may ask for changes; that's normal. Once approved, a maintainer will merge it.

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
