using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Msg Optimizer")]
[assembly: AssemblyProduct("Msg Optimizer")]
[assembly: AssemblyDescription("Adds 'Optimize Text' to the right-click menu of any Windows app.")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace MsgOptimizer
{
    /// Entry point: one instance per user session; the menu runs on this (UI) thread, the hooks on their own.
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Native.EnableDpiAwareness();                                       // hook coordinates are physical pixels
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // a csc build otherwise defaults to TLS 1.0
            if (args.Contains("--selftest")) return SelfTest.Run(args.Contains("--ping"));
            if (args.Contains("--settings"))
            {
                // Just the Settings window, e.g. from a shortcut; a running instance picks the changes up.
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new SettingsForm(Tray.AppIcon())) form.ShowDialog();
                return 0;
            }

            bool firstInstance;
            using (new Mutex(true, @"Local\MsgOptimizer.SingleInstance", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("Msg Optimizer is already running. Look for its icon in the notification area.", "Msg Optimizer");
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) =>
                {
                    Log.Error("Unhandled error", e.Exception);
                    Toast.Show("Something went wrong: " + e.Exception.Message, 6000);
                };

                Log.Info("Msg Optimizer " + Application.ProductVersion + " starting");
                MenuController.Initialize();
                Tray.Initialize();
                InputHook.Start();
                Application.Run();
                Tray.Shutdown();
                Log.Info("Exited");
            }
            return 0;
        }
    }
}
