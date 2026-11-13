using System;
using System.IO;
using System.Text;
using EzLink;

namespace ezLinkSetup
{
    /// <summary>
    /// The helper's own log, %ALLUSERSPROFILE%\ezLink\setup.log, which the plan names as the place failures are
    /// reported (the helper has no console). Every line is also sent to the normal ezLink log. Never throws.
    /// </summary>
    internal static class SetupLog
    {
        private static readonly object sync = new object();

        /// <summary>The shared data folder, %ALLUSERSPROFILE%\ezLink.</summary>
        public static string DataDirectory
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("ALLUSERSPROFILE");
                if (string.IsNullOrEmpty(root))
                {
                    root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                }
                return Path.Combine(root, Constants.AppFolderName);
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(DataDirectory, "setup.log"); }
        }

        public static void Info(string format, params object[] args)
        {
            Write("INFO ", format, args);
            Log.Info(format, args);
        }

        public static void Error(string format, params object[] args)
        {
            Write("ERROR", format, args);
            Log.Error(format, args);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", "{0}: {1}", new object[] { message, ex });
            Log.Error(message, ex);
        }

        private static void Write(string level, string format, object[] args)
        {
            try
            {
                string message = (args != null && args.Length > 0) ? string.Format(format, args) : format;
                lock (sync)
                {
                    Directory.CreateDirectory(DataDirectory);
                    using (FileStream stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine("{0:yyyy-MM-dd HH:mm:ss} {1} {2}", DateTime.Now, level, message);
                    }
                }
            }
            catch
            {
                // Logging must never make a verb fail.
            }
        }
    }
}
