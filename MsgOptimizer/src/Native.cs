using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MsgOptimizer
{
    /// Win32 interop. Everything the rest of the app needs from user32/shcore/dwmapi.
    static class Native
    {
        public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        public const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
        public const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202,
            WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MBUTTONDOWN = 0x0207,
            WM_MOUSEWHEEL = 0x020A, WM_XBUTTONDOWN = 0x020B, WM_MOUSEHWHEEL = 0x020E;
        /// Marks the keystrokes we send, so our own keyboard hook can tell them apart from the user's.
        public static readonly IntPtr OwnInputTag = (IntPtr)0x4D534F50;
        public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_ESCAPE = 0x1B, VK_C = 0x43, VK_V = 0x56;

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extraInfo; }

        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extraInfo; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }

        public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern bool AttachConsole(int pid);

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc, int x, int y);

        static POINT P(Point p) { return new POINT { X = p.X, Y = p.Y }; }

        /// Top-level window under a screen point (physical pixels).
        public static IntPtr RootAt(Point p) { return Root(WindowFromPoint(P(p))); }
        public static IntPtr Root(IntPtr hwnd) { return hwnd == IntPtr.Zero ? hwnd : GetAncestor(hwnd, 2 /* GA_ROOT */); }
        public static uint Pid(IntPtr hwnd) { uint pid; GetWindowThreadProcessId(hwnd, out pid); return pid; }
        public static string ClassName(IntPtr hwnd) { var sb = new StringBuilder(256); GetClassName(hwnd, sb, sb.Capacity); return sb.ToString(); }
        public static Rectangle Rect(IntPtr hwnd) { RECT r; return GetWindowRect(hwnd, out r) ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom) : Rectangle.Empty; }

        /// Presses the keys in order, then releases them in reverse: Keys(VK_CONTROL, VK_C) is Ctrl+C.
        public static void Keys(params ushort[] vks)
        {
            var inputs = new INPUT[vks.Length * 2];
            for (int i = 0; i < vks.Length; i++)
            {
                inputs[i] = Key(vks[i], false);
                inputs[inputs.Length - 1 - i] = Key(vks[i], true);
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static INPUT Key(ushort vk, bool up)
        {
            var input = new INPUT { type = 1 /* INPUT_KEYBOARD */ };
            input.u.ki.wVk = vk;
            input.u.ki.dwFlags = up ? 2u /* KEYEVENTF_KEYUP */ : 0u;
            input.u.ki.extraInfo = OwnInputTag;
            return input;
        }

        /// Hook coordinates are physical pixels; without this, placement and hit-testing drift on scaled displays.
        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext((IntPtr)(-4) /* PER_MONITOR_AWARE_V2 */)) return; }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }

        public static float ScaleAt(Point p)
        {
            try
            {
                uint x, y;
                if (GetDpiForMonitor(MonitorFromPoint(P(p), 2 /* NEAREST */), 0 /* EFFECTIVE */, out x, out y) == 0) return x / 96f;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            return 1f;
        }

        /// Colour of a screen pixel (physical coordinates), or null if it can't be read.
        public static Color? ScreenPixel(Point p)
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            try
            {
                uint c = GetPixel(dc, p.X, p.Y);
                return c == 0xFFFFFFFF ? (Color?)null : Color.FromArgb((int)(c & 0xFF), (int)((c >> 8) & 0xFF), (int)((c >> 16) & 0xFF));
            }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        public static void TopMost(IntPtr hwnd)
        {
            SetWindowPos(hwnd, (IntPtr)(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /* NOSIZE | NOMOVE | NOACTIVATE */);
        }

        /// Windows 11 rounded corners; a no-op on Windows 10.
        public static void RoundCorners(IntPtr hwnd)
        {
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
        }
    }
}
