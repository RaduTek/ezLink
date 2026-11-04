using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace EzLink
{
    /// <summary>
    /// Start-up shared by all three executables: logging, unhandled-exception
    /// hooks and a log line describing the runtime the process ended up on.
    /// </summary>
    public static class AppBootstrap
    {
        /// <summary>Call first thing in Main, before any window is created.</summary>
        public static void Start(string processName, string[] args)
        {
            Log.Init(processName);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Log.Info("{0} {1} starting", processName, Assembly.GetEntryAssembly().GetName().Version);
            Log.Info("OS {0}; CLR {1}; {2}-bit process; user {3}",
                Environment.OSVersion.VersionString,
                Environment.Version,
                IntPtr.Size * 8,
                Environment.UserName);
            Log.Info("Command line: {0}", Environment.CommandLine);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Error("Unhandled UI-thread exception", e.Exception);
            MessageBox.Show(
                string.Format(Strings.Get("UnexpectedError"), e.Exception.Message, Log.LogDirectory),
                Strings.Get("AppName"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled exception (terminating={0}): {1}", e.IsTerminating, e.ExceptionObject);
        }
    }
}
