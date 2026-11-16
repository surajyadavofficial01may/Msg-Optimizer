# MsgOptimizer (Windows app source)

This folder holds the source of the Msg Optimizer Windows app.

- **What it is, how to install and use it:** [../README.md](../README.md)
- **How it works, test record, limitations:** [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)
- **How to contribute:** [../CONTRIBUTING.md](../CONTRIBUTING.md)

## Build

```bat
build.cmd
```

This compiles `bin\MsgOptimizer.exe` with the C# compiler that ships with Windows (.NET Framework 4.8; no SDK needed), then runs the self-test. Run it from Command Prompt or PowerShell; if you double-click it, the window closes before you can read the result.

| Command | What it does |
| --- | --- |
| `bin\MsgOptimizer.exe` | Start the app |
| `bin\MsgOptimizer.exe --settings` | Open only the Settings window |
| `bin\MsgOptimizer.exe --selftest [--ping]` | Logic checks; `--ping` also sends one real request |
