using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using DPoint = System.Drawing.Point;

namespace MsgOptimizer
{
    /// What UI Automation could tell about the current selection.
    enum SelState { Unknown, Empty, Text }

    /// What UI Automation could tell about the selection: Text, Empty (the app exposes
    /// text and nothing is selected) or Unknown (no text pattern found, error, or timeout).
    sealed class Probe
    {
        public readonly SelState State;
        public readonly string Text;
        public Probe(SelState state, string text) { State = state; Text = text ?? ""; }
        public static readonly Probe Unknown = new Probe(SelState.Unknown, "");
    }

    /// Reading the selection (UI Automation, clipboard) and writing it back (clipboard + Ctrl+V).
    static class Selection
    {
        const int MaxChars = 20000;

        /// The first UI Automation call loads a lot of machinery; do it before the first right-click.
        public static void WarmUp()
        {
            Task.Run(() => { try { var root = AutomationElement.RootElement; } catch { } });
        }

        /// Never throws. Runs off the UI thread and gives up after timeoutMs.
        public static async Task<Probe> ProbeAsync(DPoint pt, int timeoutMs)
        {
            var work = Task.Run(() => ProbeNow(pt));
            if (await Task.WhenAny(work, Task.Delay(timeoutMs)) != work) return Probe.Unknown;
            return work.Result;
        }

        /// Chromium/Electron/WebView2 (Chrome, Teams, WhatsApp), Word and Notepad are expected to
        /// expose a TextPattern on the edit field or document. Starting from the element under the
        /// cursor, then from the focused element, the nearest TextPattern up the tree decides.
        static Probe ProbeNow(DPoint pt)
        {
            bool sawTextPattern = false;
            Func<AutomationElement>[] starts =
            {
                () => AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y)),
                () => AutomationElement.FocusedElement,
            };
            foreach (var start in starts)
            {
                try
                {
                    var el = start();
                    for (int depth = 0; el != null && depth < 12; depth++, el = TreeWalker.RawViewWalker.GetParent(el))
                    {
                        object pattern;
                        if (!el.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) continue;
                        sawTextPattern = true;
                        string text = string.Concat(((TextPattern)pattern).GetSelection().Select(r => r.GetText(MaxChars)));
                        if (text.Trim().Length > 0) return new Probe(SelState.Text, text);
                        break;
                    }
                }
                catch { /* element went away, provider error, or a higher-integrity app: try the next start */ }
            }
            return sawTextPattern ? new Probe(SelState.Empty, "") : Probe.Unknown;
        }

        /// Sends Ctrl+C to the foreground app and waits for the clipboard to change. Null if it didn't.
        public static async Task<string> CopySelectionAsync()
        {
            uint before = Native.GetClipboardSequenceNumber();
            Native.Keys(Native.VK_CONTROL, Native.VK_C);
            for (int i = 0; i < 40 && Native.GetClipboardSequenceNumber() == before; i++) await Task.Delay(25);
            if (Native.GetClipboardSequenceNumber() == before) return null;
            // The sequence number moves on EmptyClipboard, before the source app has written its data.
            for (int i = 0; i < 10; i++)
            {
                try { if (Clipboard.ContainsText()) return Clipboard.GetText(); }
                catch { /* the source app still has the clipboard open */ }
                await Task.Delay(30);
            }
            return null;
        }

        /// Copies every clipboard format we can read so the user's clipboard can be put back.
        // ponytail: best effort; exotic OLE formats (embedded Office objects) may not survive the round trip.
        public static IDataObject BackupClipboard()
        {
            try
            {
                var src = Clipboard.GetDataObject();
                var copy = new DataObject();
                if (src == null) return copy;
                foreach (var format in src.GetFormats(false))
                {
                    try { var data = src.GetData(format, false); if (data != null) copy.SetData(format, false, data); }
                    catch { }
                }
                return copy;
            }
            catch { return null; }
        }

        /// Puts back a snapshot from BackupClipboard. A null snapshot leaves the clipboard alone.
        public static void RestoreClipboard(IDataObject saved)
        {
            if (saved == null) return; // couldn't read it, so don't clobber what's there
            try
            {
                if (saved.GetFormats().Length == 0) Clipboard.Clear();
                else Clipboard.SetDataObject(saved, true, 10, 50);
            }
            catch { }
        }

        /// Replaces the clipboard with plain Unicode text, retrying while another app holds it.
        public static void SetClipboardText(string text)
        {
            Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, text), true, 10, 50);
        }
    }
}
