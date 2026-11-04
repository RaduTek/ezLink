using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace EzLink
{
    /// <summary>
    /// Minimal file logger. One file per process name and day under
    /// %LOCALAPPDATA%\ezLink\logs (on XP the folder resolves to
    /// %USERPROFILE%\Local Settings\Application Data). Logging never throws.
    /// </summary>
    public static class Log
    {
        private const int KeepDays = 14;

        private static readonly object sync = new object();
        private static string processName = "ezLink";

        /// <summary>The folder the log files are written to.</summary>
        public static string LogDirectory
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (root == null || root.Length == 0)
                {
                    root = Path.GetTempPath();
                }
                return Path.Combine(Path.Combine(root, Constants.AppFolderName), "logs");
            }
        }

        /// <summary>Sets the log file prefix (e.g. "ezLink") and removes logs older than two weeks.</summary>
        public static void Init(string name)
        {
            lock (sync)
            {
                processName = name;
            }
            try
            {
                string dir = LogDirectory;
                Directory.CreateDirectory(dir);
                DateTime limit = DateTime.UtcNow.AddDays(-KeepDays);
                foreach (string file in Directory.GetFiles(dir, "*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) < limit)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ezLink log init failed: " + ex.Message);
            }
        }

        public static void Info(string format, params object[] args)
        {
            Write("INFO ", format, args);
        }

        public static void Warn(string format, params object[] args)
        {
            Write("WARN ", format, args);
        }

        public static void Error(string format, params object[] args)
        {
            Write("ERROR", format, args);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", "{0}: {1}", new object[] { message, ex });
        }

        private static void Write(string level, string format, object[] args)
        {
            try
            {
                string message = (args != null && args.Length > 0) ? string.Format(format, args) : format;
                DateTime now = DateTime.Now;
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}:{2}] {3} {4}",
                    now,
                    Process.GetCurrentProcess().Id,
                    Thread.CurrentThread.ManagedThreadId,
                    level,
                    message);
                Debug.WriteLine(line);

                lock (sync)
                {
                    string dir = LogDirectory;
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, string.Format("{0}-{1:yyyyMMdd}.log", processName, now));
                    using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            catch
            {
                // Logging must never take the application down.
            }
        }
    }
}
