using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// Runs ezLinkSetup.exe, the only part of ezLink that needs administrator rights, with a UAC prompt on
    /// Vista and later. ezLink itself never runs elevated.
    /// </summary>
    internal static class SetupLauncher
    {
        private const int ErrorCancelled = 1223; // the user declined the UAC prompt

        /// <summary>
        /// Runs the setup helper with the given verb and waits for it. Returns false if it could not be run or
        /// the user declined elevation (the user has been told, except for a declined prompt); otherwise true
        /// with the helper's exit code.
        /// </summary>
        public static bool Run(string arguments, IWin32Window owner, out int exitCode)
        {
            exitCode = -1;
            string exe = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ezLinkSetup.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(owner, Strings.Get("SetupMissing"), Strings.Get("AppName"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            ProcessStartInfo info = new ProcessStartInfo(exe, arguments);
            info.UseShellExecute = true;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                info.Verb = "runas"; // on XP "runas" would ask for credentials; the helper's manifest is enough there
            }

            try
            {
                Log.Info("Running the setup helper: {0}", arguments);
                using (Process process = Process.Start(info))
                {
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }
                Log.Info("The setup helper exited with {0}", exitCode);
                return true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != ErrorCancelled)
                {
                    Log.Error("Could not start the setup helper", ex);
                    MessageBox.Show(owner, ex.Message, Strings.Get("AppName"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }
        }
    }
}
