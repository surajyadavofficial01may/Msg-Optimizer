using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MsgOptimizer
{
    /// Close the app's menu → copy the selection → rewrite it → paste it back → confirm.
    ///
    /// The user's clipboard is put back only once the replacement is confirmed. If the app won't
    /// take the paste, or the user moved on while the model was working, the result is left on
    /// the clipboard instead, so it is never lost.
    static class TextReplacer
    {
        // Timing knobs. Raise PasteSettleMs if a slow app reports "didn't accept the paste" but did paste.
        const int MenuCloseMs = 120;    // after Esc, before Ctrl+C
        const int PasteSettleMs = 400;  // after Ctrl+V, before checking the selection was replaced
        const int MaxChars = 20000;

        /// True while a rewrite is in progress; the menu isn't offered again until it finishes.
        public static bool Busy { get; private set; }

        /// Runs one action on the selection in window. Never throws: every outcome ends in a status message.
        public static async void Run(Settings settings, ActionDef action, IntPtr window, Point clickPt, IntPtr hostMenu)
        {
            Busy = true;
            IDataObject saved = null;
            bool restore = false; // put the user's clipboard back at the end?
            try
            {
                // Our menu was still open, so no other click or key happened since the right-click:
                // the app's menu is still open too (we swallowed the click). Close it.
                Native.Keys(Native.VK_ESCAPE);
                await Task.Delay(MenuCloseMs);
                if (hostMenu != IntPtr.Zero && Native.IsWindowVisible(hostMenu))
                {
                    Log.Info("App menu still open after Esc; sending a second Esc");
                    Native.Keys(Native.VK_ESCAPE); // a native submenu was open: Esc closes one level at a time
                    await Task.Delay(MenuCloseMs);
                }
                if (Native.Root(Native.GetForegroundWindow()) != window)
                {
                    Toast.Show("That window is no longer in front. Nothing was changed.");
                    return;
                }

                saved = Selection.BackupClipboard();
                restore = true;
                string text = await Selection.CopySelectionAsync();
                if (string.IsNullOrWhiteSpace(text))
                {
                    Probe p = await Selection.ProbeAsync(clickPt, 800); // apps that block Ctrl+C
                    text = p.State == SelState.Text ? p.Text : null;
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    Log.Info("No text captured");
                    Toast.Show("No selected text found. (For apps running as administrator, run Msg Optimizer as administrator too.)", 6000);
                    return;
                }
                if (text.Length > MaxChars)
                {
                    Toast.Show("The selection is too long (over " + MaxChars.ToString("N0") + " characters).");
                    return;
                }

                Toast.Show(action.Label + "…", 0);
                int mark = InputHook.UserInput;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                string result = await LlmClient.RewriteAsync(settings, action, text);
                Log.Info("Rewrote " + text.Length + " -> " + result.Length + " chars in " + clock.ElapsedMilliseconds + " ms" + (settings.IsDemo ? " (demo)" : ""));

                Selection.SetClipboardText(result);
                restore = false; // from here on the result stays on the clipboard unless replacement is confirmed
                if (InputHook.UserInput != mark || Native.Root(Native.GetForegroundWindow()) != window)
                {
                    Log.Info("User moved on; result left on the clipboard");
                    Toast.Show("You moved on while it was working, so nothing was replaced. The result is copied: press Ctrl+V to paste it.", 6000);
                    return;
                }

                Native.Keys(Native.VK_CONTROL, Native.VK_V);
                await Task.Delay(PasteSettleMs);
                Probe after = await Selection.ProbeAsync(clickPt, 600);
                string demo = settings.IsDemo ? " Demo mode: add an API key in Settings." : "";
                Log.Info("After paste the selection is " + after.State);
                if (after.State == SelState.Empty)
                {
                    restore = true; // the paste landed (the selection collapsed), so the clipboard is free again
                    Toast.Show("Done. Press Ctrl+Z to undo." + demo, 4000);
                }
                else if (after.State == SelState.Text && Same(after.Text, text))
                    Toast.Show("This app didn't accept the paste. The result is copied: press Ctrl+V where you want it.", 6000);
                else
                    Toast.Show("Done. The result is also on your clipboard." + demo, 4000);
            }
            catch (Exception ex)
            {
                Log.Error("Rewrite failed", ex);
                Toast.Show("Couldn't optimize: " + ex.Message, 7000);
            }
            finally
            {
                if (restore) Selection.RestoreClipboard(saved);
                Busy = false;
            }
        }

        static bool Same(string a, string b)
        {
            return Regex.Replace(a, @"\s+", " ").Trim() == Regex.Replace(b, @"\s+", " ").Trim();
        }
    }
}
