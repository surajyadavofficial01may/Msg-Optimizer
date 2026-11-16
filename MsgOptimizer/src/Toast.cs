using System.Drawing;
using System.Windows.Forms;

namespace MsgOptimizer
{
    /// A one-line, click-through status message where the menu was ("Correct English…", "Done").
    static class Toast
    {
        public static Point Anchor;                                 // set by MenuController when it shows the menu
        public static Color Background = MenuWindow.ThemeBackground();
        static MenuWindow window;
        static Timer timer;

        /// Hides the current message, if any.
        public static void Hide()
        {
            if (window != null) { timer.Stop(); window.Hide(); }
        }

        /// ms = 0 keeps it up until the next message.
        public static void Show(string message, int ms = 3500)
        {
            if (window == null)
            {
                window = new MenuWindow(true);
                timer = new Timer();
                timer.Tick += (s, e) => { timer.Stop(); window.Hide(); };
            }
            if (message.Length > 200) message = message.Substring(0, 200) + "…";
            float scale = Native.ScaleAt(Anchor);
            window.SetItems(new[] { new MenuWindow.Item(message, null) }, false, scale, Background, 0);
            Rectangle work = Screen.FromPoint(Anchor).WorkingArea;
            window.ShowAt(MenuController.Dock(Anchor, window.Size, work, Rectangle.Empty, (int)(4 * scale), r => true).Location);
            timer.Stop();
            if (ms > 0) { timer.Interval = ms; timer.Start(); }
        }
    }
}
