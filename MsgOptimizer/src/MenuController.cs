using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MsgOptimizer
{
    /// Immutable snapshot of where the menu is on screen. The hook thread reads it to decide,
    /// synchronously, whether a click belongs to us.
    sealed class MenuGeometry
    {
        public const int Outside = -2; // -1 = inside a panel but on its padding
        readonly Rectangle parent, child;
        readonly int childCount, itemHeight, pad;

        public MenuGeometry(Rectangle parent, Rectangle child, int childCount, int itemHeight, int pad)
        {
            this.parent = parent; this.child = child; this.childCount = childCount; this.itemHeight = itemHeight; this.pad = pad;
        }

        /// Index of the parent item under p, -1 on padding, Outside if not on the parent panel.
        public int HitParent(Point p) { return Hit(parent, 1, p); }
        /// Index of the submenu item under p, -1 on padding, Outside if not on the submenu.
        public int HitChild(Point p) { return Hit(child, childCount, p); }

        int Hit(Rectangle r, int count, Point p)
        {
            if (r.IsEmpty || !r.Contains(p)) return Outside;
            int y = p.Y - r.Top - pad;
            return y >= 0 && y / itemHeight < count ? y / itemHeight : -1;
        }
    }

    /// The "Optimize Text ›" menu: decides when to show it, docks it onto the app's own context
    /// menu so it reads as part of it, and drives hover and clicks from the global hook.
    ///
    /// No API can add items to the menus Chrome, Edge, Teams, WhatsApp or Notepad draw themselves,
    /// so the item is a separate, never-activated window placed flush against that menu.
    static class MenuController
    {
        const int ProbeTimeoutMs = 400;  // UI Automation budget per right-click
        const int HostMenuWaitMs = 300;  // how long to wait for the app to open its own menu
        const string ParentLabel = "Optimize Text", ParentIcon = "E70F";

        // Consoles (Ctrl+C would interrupt the running program), the desktop, taskbar and File Explorer.
        static readonly string[] ExcludedClasses =
        {
            "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "PseudoConsoleWindow",
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "CabinetWClass",
        };
        static readonly uint OwnPid = (uint)Process.GetCurrentProcess().Id;

        /// Where the menu is; null while hidden. Written here, read by the hook thread.
        public static volatile MenuGeometry Geometry;
        /// Off while the user has paused Msg Optimizer from the tray.
        public static bool Enabled = true;

        static MenuWindow parent, child;
        static Settings menuSettings;     // what the visible menu was built from
        static int seq;                   // bumped by every dismissal; pending async work compares it
        static Point clickPt;
        static IntPtr targetRoot, hostMenu;
        static Rectangle hostRect;          // the app menu's window when the menu was shown
        static Task<Probe> probe;
        static bool childLeft, childUp;

        // Selection hint, for apps whose text UI Automation can't see: a drag, double-click,
        // Shift+click, Shift+arrows or Ctrl+A in a window means it probably has a selection.
        static IntPtr hintRoot;
        static Point leftDownPt;
        static uint leftDownTime;
        static bool leftDownKeepsSelection;

        /// Creates the menu windows. Call once on the UI thread before the hooks start.
        public static void Initialize()
        {
            parent = new MenuWindow(false);
            child = new MenuWindow(false);
            Selection.WarmUp();
        }

        // ================= input (posted from the hook thread) =================

        public static void OnLeftDown(Point pt, uint time, bool shift)
        {
            Dismiss();
            bool doubleClick = time - leftDownTime <= Native.GetDoubleClickTime()
                && Math.Abs(pt.X - leftDownPt.X) <= Native.GetSystemMetrics(36 /* SM_CXDOUBLECLK */)
                && Math.Abs(pt.Y - leftDownPt.Y) <= Native.GetSystemMetrics(37 /* SM_CYDOUBLECLK */);
            leftDownPt = pt;
            leftDownTime = time;
            leftDownKeepsSelection = doubleClick || shift;
            if (leftDownKeepsSelection) hintRoot = Native.RootAt(pt);
        }

        public static void OnLeftUp(Point pt)
        {
            if (Math.Abs(pt.X - leftDownPt.X) > 4 || Math.Abs(pt.Y - leftDownPt.Y) > 4) hintRoot = Native.RootAt(leftDownPt);
            else if (!leftDownKeepsSelection) hintRoot = IntPtr.Zero; // a plain click clears the selection
        }

        public static void OnKey(uint vk, bool shift, bool ctrl)
        {
            Dismiss();
            bool navigation = vk >= 0x21 && vk <= 0x28; // PageUp, PageDown, End, Home, arrows
            if ((shift && navigation) || (ctrl && vk == 0x41 /* A */)) hintRoot = Native.Root(Native.GetForegroundWindow());
            else if (!(ctrl && (vk == 0x43 /* C */ || vk == 0x2D /* Insert */))) hintRoot = IntPtr.Zero; // typing replaces the selection
        }

        /// Starts the selection probe for the window under the click, before the app opens its own menu.
        public static void OnRightDown(Point pt)
        {
            Dismiss();
            probe = null;
            if (!Enabled || TextReplacer.Busy) return;
            IntPtr root = Native.RootAt(pt);
            if (root == IntPtr.Zero || Native.Pid(root) == OwnPid || ExcludedClasses.Contains(Native.ClassName(root))) return;
            clickPt = pt;
            targetRoot = root;
            probe = Selection.ProbeAsync(pt, ProbeTimeoutMs); // starts before the app's menu opens and moves focus
        }

        /// Waits for the app's menu and the probe, then shows ours if text is selected.
        public static async void OnRightUp()
        {
            if (probe == null) return;
            int my = seq;
            Task<Probe> pending = probe;
            // Wait for the app to open its own menu so ours can dock to it. In-page menus have no
            // window of their own; then this times out and ours docks to the click point.
            var clock = Stopwatch.StartNew();
            hostMenu = IntPtr.Zero;
            while (clock.ElapsedMilliseconds < HostMenuWaitMs && (hostMenu = FindHostMenu()) == IntPtr.Zero) await Task.Delay(25);
            Probe found = await pending;
            if (my != seq) return; // the user clicked, scrolled or typed in the meantime

            // UI Automation is trusted when it can see the text; the selection hint only fills in when it can't.
            bool show = found.State == SelState.Text || (found.State == SelState.Unknown && hintRoot == targetRoot);
            Log.Info("Right-click in " + Native.ClassName(targetRoot) + ": selection " + found.State + " (" + found.Text.Length + " chars), hint "
                + (hintRoot == targetRoot) + ", app menu " + Describe(hostMenu) + " after " + clock.ElapsedMilliseconds + " ms -> " + (show ? "show" : "skip"));
            if (show) ShowMenu();
        }

        /// Hover state from the hook: highlights items and opens the submenu.
        public static void OnHover(int hp, int hc)
        {
            if (!parent.Visible) return;
            if (hp == 0) ShowChild();
            parent.SetHover(hp == 0 || child.Visible ? 0 : -1);
            child.SetHover(hc);
        }

        /// A swallowed click on our menu: runs the chosen action or opens the submenu.
        public static void OnMenuClick(int hp, int hc)
        {
            if (!parent.Visible) return;
            if (hc >= 0 && hc < menuSettings.Actions.Count)
            {
                ActionDef action = menuSettings.Actions[hc];
                Log.Info("Chose '" + action.Label + "'");
                Dismiss();
                TextReplacer.Run(menuSettings, action, targetRoot, clickPt, hostMenu);
            }
            else if (hp == 0) ShowChild();
        }

        /// Hides the menu and cancels any pending show.
        public static void Dismiss()
        {
            seq++;
            if (Geometry == null && !parent.Visible) return;
            Geometry = null;
            parent.Hide();
            child.Hide();
        }

        // ================= showing =================

        static async void ShowMenu()
        {
            menuSettings = Settings.Load();
            Tray.ReportSettingsError();
            float scale = Native.ScaleAt(clickPt);
            Rectangle host = hostRect = hostMenu == IntPtr.Zero ? Rectangle.Empty : Native.Rect(hostMenu);
            Color background = SampleHostBackground(host, scale) ?? MenuWindow.ThemeBackground();

            parent.SetItems(new[] { new MenuWindow.Item(ParentLabel, ParentIcon) }, true, scale, background,
                Math.Min(VisibleWidth(host, clickPt), (int)(640 * scale)));
            child.SetItems(menuSettings.Actions.Select(a => new MenuWindow.Item(a.Label, a.Icon)).ToArray(), false, scale, background, 0);
            Rectangle work = Screen.FromPoint(clickPt).WorkingArea;
            Rectangle at = Dock(clickPt, parent.Size, work, host, (int)Math.Round(2 * scale), IsFree);
            childLeft = at.Right <= clickPt.X; // parent sits left of the click: open the submenu further left
            childUp = at.Bottom <= clickPt.Y;  // parent sits above the app's menu: grow the submenu upwards, off that menu
            Toast.Hide();
            parent.ShowAt(at.Location);
            Publish();
            Toast.Anchor = clickPt;
            Toast.Background = background;
            Log.Info("Menu shown at " + at);

            // Follow the app's menu: match its colours once it has painted (a just-opened window can
            // still show what was underneath), stay above it, and close together with it.
            int my = seq;
            for (int tick = 1; ; tick++)
            {
                await Task.Delay(100);
                if (my != seq || !parent.Visible) return;
                if (tick <= 4)
                {
                    Color? painted = SampleHostBackground(host, scale);
                    if (painted.HasValue && painted.Value.ToArgb() != background.ToArgb())
                    {
                        background = painted.Value;
                        parent.SetBackground(background);
                        child.SetBackground(background);
                        Toast.Background = background;
                    }
                }
                if (tick == 3) Native.TopMost(parent.Handle);
                if (hostMenu != IntPtr.Zero && !Native.IsWindowVisible(hostMenu)) { Dismiss(); return; }
            }
        }

        static void ShowChild()
        {
            if (child.Visible || !parent.Visible) return;
            Rectangle work = Screen.FromPoint(clickPt).WorkingArea;
            child.ShowAt(PlaceSubmenu(parent.Bounds, child.Size, childLeft, childUp, work, hostRect).Location);
            Publish();
            Log.Info("Submenu shown at " + child.Bounds);
        }

        /// Like a native submenu: beside the parent on the side away from the app's menu, growing away
        /// from it. If there's no room there, under (or over) the parent, then the other side; never
        /// over the app's menu if any spot avoids it.
        internal static Rectangle PlaceSubmenu(Rectangle parent, Size size, bool left, bool up, Rectangle work, Rectangle avoid)
        {
            const int gap = 2;
            int y = up ? parent.Bottom - size.Height : parent.Top;
            var spots = new[]
            {
                new Rectangle(left ? parent.Left - size.Width - gap : parent.Right + gap, y, size.Width, size.Height),
                new Rectangle(parent.Left, up ? parent.Top - size.Height - gap : parent.Bottom + gap, size.Width, size.Height),
                new Rectangle(left ? parent.Right + gap : parent.Left - size.Width - gap, y, size.Width, size.Height),
            };
            foreach (var r in spots)
                if (work.Contains(r) && !r.IntersectsWith(avoid)) return r;
            return Clamp(spots[0], work);
        }

        static void Publish()
        {
            Geometry = new MenuGeometry(parent.Visible ? parent.Bounds : Rectangle.Empty, child.Visible ? child.Bounds : Rectangle.Empty,
                child.Count, parent.ItemHeight, parent.Pad);
        }

        // ================= the app's own menu =================

        /// The app's context menu: a separate popup window (Win32, Chromium, Office, WinUI) anchored
        /// at the click point. In-page menus (drawn inside the app's window) give Zero.
        static IntPtr FindHostMenu()
        {
            float scale = Native.ScaleAt(clickPt);
            foreach (var d in new[] { new Size(8, 8), new Size(-8, 8), new Size(8, -8), new Size(-8, -8) })
            {
                IntPtr w = Native.RootAt(clickPt + d);
                if (w != IntPtr.Zero && w != targetRoot && Native.Pid(w) != OwnPid && IsAnchoredAt(Native.Rect(w), clickPt, scale)) return w;
            }
            return IntPtr.Zero;
        }

        /// Context menus open with a side at the click point, give or take a shadow margin, and with
        /// their top or bottom there too, unless they were shifted up or down to fit on screen (then
        /// they span the click). Word puts its first item at the cursor with a search box above it,
        /// hence the taller vertical slack. Tooltips are too small to qualify.
        internal static bool IsAnchoredAt(Rectangle r, Point pt, float scale)
        {
            int dx = (int)(24 * scale), dy = (int)(64 * scale);
            bool big = r.Width >= 100 * scale && r.Height >= 60 * scale;
            bool sideAtClick = Math.Abs(r.Left - pt.X) <= dx || Math.Abs(r.Right - pt.X) <= dx;
            bool levelWithClick = Math.Abs(r.Top - pt.Y) <= dy || Math.Abs(r.Bottom - pt.Y) <= dy || (r.Top <= pt.Y && pt.Y <= r.Bottom);
            return big && sideAtClick && levelWithClick;
        }

        /// Docks our menu onto the app menu's edge at the click point so it reads as part of that
        /// menu: on top of it when it opened downwards, under it when it opened upwards, aligned with
        /// its left (or right) side. Only the side of the click the app menu's centre lies on is used,
        /// because its window can be larger than the visible menu. If that spot is off-screen or would
        /// cover one of the app's own popups (Word's formatting toolbar), it goes beside the menu instead.
        /// Without an app menu window, the usual down-right menu is assumed.
        internal static Rectangle Dock(Point pt, Size size, Rectangle work, Rectangle host, int gap, Func<Rectangle, bool> isFree)
        {
            bool right = host.IsEmpty || host.Left + host.Width / 2 >= pt.X;
            bool down = host.IsEmpty || host.Top + host.Height / 2 >= pt.Y;
            int edge = host.IsEmpty ? pt.Y : down ? Math.Min(pt.Y, host.Top) : Math.Max(pt.Y, host.Bottom);
            var onEdge = new Rectangle(right ? pt.X : pt.X - size.Width, down ? edge - size.Height - gap : edge + gap, size.Width, size.Height);
            var beside = new Rectangle(right ? pt.X - size.Width - gap : pt.X + gap, down ? pt.Y : pt.Y - size.Height, size.Width, size.Height);
            foreach (var r in new[] { onEdge, beside })
                if (work.Contains(r) && isFree(r)) return r;
            return Clamp(onEdge, work);
        }

        /// The app menu's visible width: its window may carry an equal shadow margin on each side.
        internal static int VisibleWidth(Rectangle host, Point pt)
        {
            if (host.IsEmpty) return 0;
            bool right = host.Left + host.Width / 2 >= pt.X;
            int margin = Math.Max(0, right ? pt.X - host.Left : host.Right - pt.X);
            return margin < host.Width / 4 ? host.Width - 2 * margin : 0;
        }

        /// True unless the spot would cover the app's menu or another of its popups.
        static bool IsFree(Rectangle r)
        {
            uint targetPid = Native.Pid(targetRoot), menuPid = hostMenu == IntPtr.Zero ? 0 : Native.Pid(hostMenu);
            var samples = new[]
            {
                new Point(r.Left + 2, r.Top + 2), new Point(r.Right - 3, r.Top + 2), new Point(r.Left + r.Width / 2, r.Top + r.Height / 2),
                new Point(r.Left + 2, r.Bottom - 3), new Point(r.Right - 3, r.Bottom - 3),
            };
            foreach (var p in samples)
            {
                IntPtr w = Native.RootAt(p);
                if (w == IntPtr.Zero || w == targetRoot) continue;
                uint pid = Native.Pid(w);
                if (w == hostMenu || pid == targetPid || (menuPid != 0 && pid == menuPid)) return false;
            }
            return true;
        }

        /// The app menu's own background (Edge and Teams follow their own theme, not Windows'),
        /// sampled just inside its corner at the click point. Two of three samples must agree:
        /// one can land on an icon or on the anti-aliased edge. Null if they don't.
        static Color? SampleHostBackground(Rectangle host, float scale)
        {
            if (host.IsEmpty) return null;
            int sx = host.Left + host.Width / 2 >= clickPt.X ? 1 : -1, sy = host.Top + host.Height / 2 >= clickPt.Y ? 1 : -1;
            var colors = new[] { 10, 14, 18 }
                .Select(d => Native.ScreenPixel(new Point(clickPt.X + sx * (int)(d * scale), clickPt.Y + sy * (int)(8 * scale))))
                .Where(c => c.HasValue).Select(c => c.Value).ToArray();
            return MajorityColor(colors);
        }

        internal static Color? MajorityColor(Color[] colors)
        {
            foreach (var a in colors)
                if (colors.Count(b => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 24) >= 2) return a;
            return null;
        }

        /// Moves r the least distance needed to fit inside work.
        internal static Rectangle Clamp(Rectangle r, Rectangle work)
        {
            r.X = Math.Max(work.Left, Math.Min(r.X, work.Right - r.Width));
            r.Y = Math.Max(work.Top, Math.Min(r.Y, work.Bottom - r.Height));
            return r;
        }

        static string Describe(IntPtr w)
        {
            if (w == IntPtr.Zero) return "none";
            string process;
            try { process = Process.GetProcessById((int)Native.Pid(w)).ProcessName; }
            catch { process = "?"; }
            return process + "/" + Native.ClassName(w) + " " + Native.Rect(w);
        }
    }
}
