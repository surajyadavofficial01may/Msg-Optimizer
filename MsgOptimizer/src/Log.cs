using System;
using System.IO;

namespace MsgOptimizer
{
    /// Diagnostics in %APPDATA%\MsgOptimizer\msgoptimizer.log (tray → Open log).
    /// Records decisions, window classes and text lengths; never the selected text itself.
    static class Log
    {
        public static readonly string FilePath = Path.Combine(Settings.Folder, "msgoptimizer.log");
        const long MaxBytes = 1024 * 1024;
        static readonly object Gate = new object();

        public static void Info(string message) { Write("INFO ", message); }
        public static void Error(string message, Exception ex) { Write("ERROR", message + ": " + ex); }

        static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Settings.Folder);
                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > MaxBytes) file.Delete(); // ponytail: start over instead of rotating
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + level + " " + message + Environment.NewLine);
                }
            }
            catch { /* logging must never break the app */ }
        }
    }
}
