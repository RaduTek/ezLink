using System;
using System.Diagnostics;
using System.IO;
using EzLink;

namespace ezLinkSetup
{
    /// <summary>
    /// The /drivers and /remove-drivers verbs, run by the MSI (Setup project) on Windows versions before
    /// Windows 10. They wrap DPInst, whose exit code is a status bit field that Windows Installer would
    /// otherwise treat as a failure.
    /// </summary>
    internal static class DriverSetup
    {
        private const string DriverInf32 = "rtux86lh.INF";
        private const string DriverInf64 = "rtux64lh.INF";

        private static bool Is64BitOperatingSystem()
        {
            return IntPtr.Size == 8 ||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
        }

        /// <summary>
        /// Installs or removes the USB link adapter driver with the DPInst copy installed
        /// in the "drivers" folder next to this exe. Returns 0 unless DPInst could not be run;
        /// DPInst's own exit code is logged but not treated as an error (it reports the
        /// number of installed packages in its low byte, so success is not 0).
        /// </summary>
        public static int Run(bool install)
        {
            bool is64 = Is64BitOperatingSystem();
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "drivers");
            string dpinst = Path.Combine(root, is64 ? "dpinstx64.exe" : "dpinstx86.exe");
            string archDir = Path.Combine(root, is64 ? "x64" : "x86");

            if (!File.Exists(dpinst))
            {
                // Windows 10 and later install no drivers, so there is nothing to do.
                Log.Info("{0} not found; no driver package installed", dpinst);
                return 0;
            }

            string arguments;
            if (install)
            {
                arguments = "/Q /SE /SA /SW /PATH \"" + archDir + "\"";
            }
            else
            {
                string inf = Path.Combine(archDir, is64 ? DriverInf64 : DriverInf32);
                arguments = "/U \"" + inf + "\" /D /Q";
            }

            try
            {
                Log.Info("Running {0} {1}", dpinst, arguments);
                ProcessStartInfo info = new ProcessStartInfo(dpinst, arguments);
                info.WorkingDirectory = root;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                using (Process process = Process.Start(info))
                {
                    process.WaitForExit();
                    Log.Info("DPInst exit code 0x{0:X8}", process.ExitCode);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("Could not run DPInst", ex);
                return 1;
            }
        }
    }
}
