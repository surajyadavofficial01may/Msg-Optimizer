using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MsgOptimizer
{
    /// A borderless popup that never takes focus and draws a list of items in the style of a
    /// Windows 11 context menu, coloured to match the app menu it sits next to. It handles no
    /// mouse input itself: hover and clicks come from the global hook (see MenuController).
    sealed class MenuWindow : Form
    {
        public struct Item
        {
            public readonly string Text, Icon; // Icon: a Segoe icon-font code point in hex, e.g. "E715"; may be null
            public Item(string text, string icon) { Text = text; Icon = icon; }
        }

        static readonly string IconFontName =
            new InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        const string Chevron = "";

        readonly bool clickThrough;
        Item[] items = new Item[0];
        string[] glyphs = new string[0];
        bool submenuArrow, hasIcons;
        int hover = -1;
        float scale = 1f;
        Font font, iconFont, arrowFont;
        Color background, highlight, border, foreground;

        public int ItemHeight { get; private set; }
        public int Pad { get; private set; }
        public int Count { get { return items.Length; } }

        /// clickThrough: mouse input passes to the window underneath (the status toast).
        public MenuWindow(bool clickThrough)
        {
            this.clickThrough = clickThrough;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            if (clickThrough) Opacity = 0.97; // makes WinForms set the layered-window alpha that WS_EX_TRANSPARENT needs
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8;       // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
                if (clickThrough) cp.ExStyle |= 0x20 | 0x80000; // WS_EX_TRANSPARENT | WS_EX_LAYERED
                cp.ClassStyle |= 0x20000;                      // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        /// Windows' light or dark menu colour, used when the app's own menu couldn't be sampled.
        public static Color ThemeBackground()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                return v is int && (int)v == 0 ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);
            }
        }

        /// minWidth: e.g. the app menu's width, so ours lines up with it.
        public void SetItems(Item[] newItems, bool showSubmenuArrow, float newScale, Color newBackground, int minWidth)
        {
            items = newItems;
            submenuArrow = showSubmenuArrow;
            scale = newScale;
            hover = -1;
            SetColors(newBackground);
            glyphs = items.Select(i => Glyph(i.Icon)).ToArray();
            hasIcons = glyphs.Any(g => g != null);

            DisposeFonts();
            font = new Font("Segoe UI", 14f * scale, GraphicsUnit.Pixel);
            iconFont = new Font(IconFontName, 16f * scale, GraphicsUnit.Pixel);
            arrowFont = new Font(IconFontName, 10f * scale, GraphicsUnit.Pixel);
            ItemHeight = (int)Math.Round(32 * scale);
            Pad = (int)Math.Round(4 * scale);

            int textWidth = items.Length == 0 ? 0 : items.Max(i => TextRenderer.MeasureText(i.Text, font).Width);
            int chrome = TextLeft() + (int)((submenuArrow ? 44 : 20) * scale);
            int menuMin = clickThrough ? 0 : (int)(200 * scale); // menus get a menu-like width; status messages fit their text
            Size = new Size(Math.Max(Math.Max(minWidth, menuMin), textWidth + chrome), items.Length * ItemHeight + 2 * Pad);
            Invalidate();
        }

        /// Recolours the window, deriving hover, border and text colours from the background.
        public void SetBackground(Color newBackground)
        {
            SetColors(newBackground);
            Invalidate();
        }

        /// Highlights one item; -1 for none.
        public void SetHover(int index)
        {
            if (index < 0) index = -1;
            if (index == hover) return;
            hover = index;
            Invalidate();
        }

        /// Shows the window at location (physical pixels) without activating it, above other topmost windows.
        public void ShowAt(Point location)
        {
            Location = location;
            if (!Visible) Show();
            Native.TopMost(Handle); // above the app's menu, which is topmost too
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(background);
            using (var pen = new Pen(border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            const TextFormatFlags centered = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            int iconLeft = Pad + (int)(12 * scale), textLeft = TextLeft();
            for (int i = 0; i < items.Length; i++)
            {
                var row = new Rectangle(Pad, Pad + i * ItemHeight, Width - 2 * Pad, ItemHeight);
                if (i == hover) FillRounded(g, row, (int)(4 * scale), highlight);
                if (glyphs[i] != null)
                    TextRenderer.DrawText(g, glyphs[i], iconFont, new Rectangle(iconLeft, row.Y, (int)(20 * scale), row.Height), foreground, centered | TextFormatFlags.HorizontalCenter);
                var text = new Rectangle(textLeft, row.Y, row.Right - textLeft - (int)((submenuArrow ? 32 : 12) * scale), row.Height);
                TextRenderer.DrawText(g, items[i].Text, font, text, foreground, centered | TextFormatFlags.EndEllipsis);
                if (submenuArrow)
                    TextRenderer.DrawText(g, Chevron, arrowFont, new Rectangle(row.Right - (int)(28 * scale), row.Y, (int)(16 * scale), row.Height), foreground, centered | TextFormatFlags.HorizontalCenter);
            }
        }

        int TextLeft() { return Pad + (int)((hasIcons ? 44 : 12) * scale); }

        void SetColors(Color bg)
        {
            background = Color.FromArgb(255, bg);
            bool dark = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B < 128;
            highlight = Blend(bg, dark ? Color.White : Color.Black, dark ? 0.10f : 0.07f);
            border = Blend(bg, dark ? Color.White : Color.Black, dark ? 0.16f : 0.13f);
            foreground = dark ? Color.White : Color.FromArgb(26, 26, 26);
        }

        static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static string Glyph(string hex)
        {
            int code;
            return !string.IsNullOrEmpty(hex) && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code) && code > 0 && code <= 0xFFFF
                ? ((char)code).ToString() : null;
        }

        static void FillRounded(Graphics g, Rectangle r, int radius, Color color)
        {
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(color))
            {
                int d = radius * 2;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                var mode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
                g.SmoothingMode = mode;
            }
        }

        void DisposeFonts()
        {
            foreach (var f in new[] { font, iconFont, arrowFont }) if (f != null) f.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeFonts();
            base.Dispose(disposing);
        }
    }
}
