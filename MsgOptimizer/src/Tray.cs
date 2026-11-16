using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MsgOptimizer
{
    /// The notification-area icon and its menu.
    static class Tray
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunValue = "MsgOptimizer";
        static NotifyIcon icon;
        static SettingsForm settingsForm;

        /// Creates the tray icon and its menu, and shows the first-run tip.
        public static void Initialize()
        {
            Settings settings = Settings.Load(); // writes settings.json on first run
            var menu = new ContextMenuStrip();
            menu.Items.Add("Msg Optimizer").Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            var enabled = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
            enabled.CheckedChanged += (s, e) => { MenuController.Enabled = enabled.Checked; MenuController.Dismiss(); };
            menu.Items.Add(enabled);
            menu.Items.Add("Settings…", null, (s, e) => ShowSettings());
            menu.Items.Add("Open log", null, (s, e) => { if (File.Exists(Log.FilePath)) Process.Start("notepad.exe", "\"" + Log.FilePath + "\""); });
            var autostart = new ToolStripMenuItem("Start with Windows") { Checked = Autostart, CheckOnClick = true };
            autostart.CheckedChanged += (s, e) => Autostart = autostart.Checked;
            menu.Items.Add(autostart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Application.Exit());

            icon = new NotifyIcon { Icon = AppIcon(), Text = "Msg Optimizer", ContextMenuStrip = menu, Visible = true };
            icon.DoubleClick += (s, e) => ShowSettings();
            icon.ShowBalloonTip(6000, "Msg Optimizer is running",
                "Select text in any app and right-click: 'Optimize Text' appears on top of the app's menu."
                + (settings.IsDemo ? "\nDemo mode: double-click this icon to add an API key." : ""), ToolTipIcon.Info);
        }

        /// Removes the icon so it doesn't linger in the notification area after exit.
        public static void Shutdown()
        {
            if (icon != null) icon.Visible = false;
        }

        /// Called when the menu reloads settings.json: a broken file is reported once, as a balloon.
        public static void ReportSettingsError()
        {
            if (Settings.LastError == null) return;
            Log.Info("settings.json error: " + Settings.LastError);
            icon.ShowBalloonTip(6000, "settings.json has an error", Settings.LastError + "\nUsing the previous settings.", ToolTipIcon.Warning);
            Settings.LastError = null;
        }

        static void ShowSettings()
        {
            if (settingsForm != null) { settingsForm.Activate(); return; }
            using (settingsForm = new SettingsForm(icon.Icon)) settingsForm.ShowDialog();
            settingsForm = null;
        }

        static bool Autostart
        {
            get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunValue) != null; }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunValue, false);
                }
            }
        }

        /// The app icon, from the embedded app.png.
        public static Icon AppIcon()
        {
            using (var stream = typeof(Tray).Assembly.GetManifestResourceStream("app.png"))
            {
                if (stream == null) return SystemIcons.Application;
                using (var bmp = new Bitmap(stream)) return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
