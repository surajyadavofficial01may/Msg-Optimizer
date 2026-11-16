using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MsgOptimizer
{
    /// Global low-level mouse and keyboard hooks.
    ///
    /// They run on a dedicated thread that does nothing else, because Windows silently removes
    /// a low-level hook whose thread is slow to answer. The callback decides synchronously
    /// whether a click belongs to our menu (and swallows it); everything else is posted to the
    /// UI thread. Right-clicks always pass through, so the app's own menu still opens.
    static class InputHook
    {
        static Control ui;                          // BeginInvoke target on the UI thread
        static Native.HookProc mouseProc, keyProc;  // fields keep the delegates alive for the hooks' lifetime
        static IntPtr mouseHook, keyHook;
        static bool swallowLeftUp, swallowRightUp;
        static int lastHoverParent = -3, lastHoverChild = -3;
        static int userInput;

        /// Count of real clicks and key presses (not our own keystrokes). A change means the user did something.
        public static int UserInput { get { return Thread.VolatileRead(ref userInput); } }

        /// Call on the UI thread.
        public static void Start()
        {
            ui = new Control();
            IntPtr forceHandle = ui.Handle;
            var thread = new Thread(Run) { IsBackground = true, Name = "Input hooks" };
            thread.SetApartmentState(ApartmentState.STA); // it runs a WinForms message loop
            thread.Start();
        }

        static void Run()
        {
            mouseProc = MouseProc;
            keyProc = KeyProc;
            IntPtr module = Native.GetModuleHandle(null);
            mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, module, 0);
            keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyProc, module, 0);
            if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Post(() =>
                {
                    Log.Info("Hook installation failed, Win32 error " + error);
                    MessageBox.Show("Couldn't install the mouse/keyboard hook (Win32 error " + error + ").", "Msg Optimizer");
                    Application.Exit();
                });
                return;
            }
            Application.Run(); // the hook callbacks are delivered through this message loop
        }

        static void Post(Action action)
        {
            try { ui.BeginInvoke(action); }
            catch (InvalidOperationException) { /* shutting down */ }
        }

        static IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                try
                {
                    // Injected mouse input counts too, so remote-control tools and scripted clicks work.
                    var m = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    if (OnMouse((int)wParam, new Point(m.pt.X, m.pt.Y), m.time)) return (IntPtr)1; // swallowed
                }
                catch { /* an exception must never escape into the hook chain */ }
            }
            return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        /// Returns true to swallow the event.
        static bool OnMouse(int msg, Point pt, uint time)
        {
            MenuGeometry g = MenuController.Geometry;
            int hp = g == null ? MenuGeometry.Outside : g.HitParent(pt);
            int hc = g == null ? MenuGeometry.Outside : g.HitChild(pt);
            bool onMenu = hp != MenuGeometry.Outside || hc != MenuGeometry.Outside;
            switch (msg)
            {
                case Native.WM_MOUSEMOVE:
                    if (g == null) { lastHoverParent = lastHoverChild = -3; return false; }
                    if (hp != lastHoverParent || hc != lastHoverChild)
                    {
                        lastHoverParent = hp;
                        lastHoverChild = hc;
                        Post(() => MenuController.OnHover(hp, hc));
                    }
                    return false;

                case Native.WM_LBUTTONDOWN:
                    // While the app's own menu is open it holds mouse capture, so our window would
                    // never see this click. Swallowing it also keeps that menu open until we close it.
                    if (onMenu) { swallowLeftUp = true; return true; }
                    Interlocked.Increment(ref userInput);
                    bool shift = Native.GetAsyncKeyState(Native.VK_SHIFT) < 0;
                    Post(() => MenuController.OnLeftDown(pt, time, shift));
                    return false;

                case Native.WM_LBUTTONUP:
                    if (swallowLeftUp) { swallowLeftUp = false; Post(() => MenuController.OnMenuClick(hp, hc)); return true; }
                    Post(() => MenuController.OnLeftUp(pt));
                    return false;

                case Native.WM_RBUTTONDOWN:
                    if (onMenu) { swallowRightUp = true; return true; }
                    Interlocked.Increment(ref userInput);
                    Post(() => MenuController.OnRightDown(pt));
                    return false;

                case Native.WM_RBUTTONUP:
                    if (swallowRightUp) { swallowRightUp = false; return true; }
                    Post(MenuController.OnRightUp);
                    return false;

                case Native.WM_MBUTTONDOWN:
                case Native.WM_XBUTTONDOWN:
                    Interlocked.Increment(ref userInput);
                    Post(MenuController.Dismiss);
                    return false;

                case Native.WM_MOUSEWHEEL:
                case Native.WM_MOUSEHWHEEL:
                    if (g != null) Post(MenuController.Dismiss);
                    return false;
            }
            return false;
        }

        static IntPtr KeyProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && ((int)wParam == Native.WM_KEYDOWN || (int)wParam == Native.WM_SYSKEYDOWN))
            {
                try
                {
                    var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    uint vk = k.vkCode;
                    bool modifier = vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C || vk == 0x14;
                    if (k.extraInfo != Native.OwnInputTag && !modifier) // skip only our own Esc / Ctrl+C / Ctrl+V
                    {
                        Interlocked.Increment(ref userInput);
                        bool shift = Native.GetAsyncKeyState(Native.VK_SHIFT) < 0, ctrl = Native.GetAsyncKeyState(Native.VK_CONTROL) < 0;
                        Post(() => MenuController.OnKey(vk, shift, ctrl));
                    }
                }
                catch { }
            }
            return Native.CallNextHookEx(keyHook, code, wParam, lParam);
        }
    }
}
