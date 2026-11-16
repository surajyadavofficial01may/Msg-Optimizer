## Summary

<!-- What does this change, and why? Link the issue if there is one: "Fixes #123". -->

## Type of change

- [ ] Bug fix
- [ ] New feature or action
- [ ] Refactor (no behaviour change)
- [ ] Documentation

## How was it tested?

- [ ] `MsgOptimizer\build.cmd` passes (it runs the self-test)
- [ ] Added or updated a check in `src/SelfTest.cs` for new pure logic
- [ ] Tested by hand in these apps (demo mode is fine for UI changes):

| App | Result |
| --- | --- |
| | |

## Checklist

- [ ] Compiles with the in-box C# 5 compiler (no string interpolation, `?.`, `=>` members, …)
- [ ] No new dependencies
- [ ] The input hook thread is never blocked
- [ ] No selected text, keystrokes or API keys in logs
- [ ] The rewrite can't be lost (it stays on the clipboard when replacement is uncertain)
- [ ] `README.md` / `docs/ARCHITECTURE.md` updated if behaviour or setup changed

## Screenshots

<!-- For anything visual (menu placement, colours, Settings window): before/after screenshots with private text hidden. -->
