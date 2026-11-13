using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using EzLink;

namespace ezLinkSetup
{
    /// <summary>
    /// Admin-only helper (requireAdministrator manifest). Verbs (each idempotent): /install (= /uuid,
    /// /firewall, /datadir), /uuid, /regenerate-uuid, /firewall, /datadir, /uninstall [/purge]. Exit code 0
    /// on success; otherwise non-zero with the reason in %ALLUSERSPROFILE%\ezLink\setup.log.
    /// /drivers and /remove-drivers are run by the MSI (Setup project) on Windows
    /// versions before Windows 10; they wrap DPInst, whose exit code is a status
    /// bit field that Windows Installer would otherwise treat as a failure.
    /// </summary>
    internal static class Program
    {
        private const string DriverInf32 = "rtux86lh.INF";
        private const string DriverInf64 = "rtux64lh.INF";

        [STAThread]
        private static int Main(string[] args)
        {
            AppBootstrap.Start("ezLinkSetup", args);

            if (args.Length == 0)
            {
                MessageBox.Show(Strings.Get("SetupPlaceholder"), Strings.Get("SetupTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            bool purge = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLowerInvariant() == "/purge")
                {
                    purge = true;
                }
            }

            int result = 0;
            for (int i = 0; i < args.Length; i++)
            {
                string verb = args[i].ToLowerInvariant();
                if (verb == "/purge")
                {
                    continue; // a modifier of /uninstall, handled above
                }
                result |= RunVerb(verb, purge);
            }

            if (result == 0)
            {
                SetupLog.Info("Setup finished: {0}", string.Join(" ", args));
            }
            else
            {
                SetupLog.Error("Setup finished with errors (exit code {0}): {1}", result, string.Join(" ", args));
            }
            return result;
        }

        /// <summary>Runs one verb. Returns 0, or a non-zero bit for the failure (1 = failed, 2 = unknown verb, 4 = reserved).</summary>
        private static int RunVerb(string verb, bool purge)
        {
            try
            {
                switch (verb)
                {
                    case "/install":
                        MachineIdSetup.Ensure();
                        FirewallSetup.Install();
                        DataDirSetup.Ensure();
                        return 0;
                    case "/uuid":
                        MachineIdSetup.Ensure();
                        return 0;
                    case "/regenerate-uuid":
                        MachineIdSetup.Regenerate();
                        return 0;
                    case "/firewall":
                        FirewallSetup.Install();
                        return 0;
                    case "/datadir":
                        DataDirSetup.Ensure();
                        return 0;
                    case "/uninstall":
                        FirewallSetup.Uninstall();
                        if (purge)
                        {
                            MachineIdSetup.Remove();
                            DataDirSetup.Purge();
                        }
                        return 0;
                    case "/drivers":
                        return DriverSetup.Run(true);
                    case "/remove-drivers":
                        return DriverSetup.Run(false);
                }

                if (verb.StartsWith("/static-ip"))
                {
                    SetupLog.Error("{0} is reserved and not available in this version", verb);
                    return 4;
                }
                SetupLog.Error("Unknown verb {0}", verb);
                return 2;
            }
            catch (Exception ex)
            {
                SetupLog.Error("Verb " + verb + " failed", ex);
                return 1;
            }
        }

    }
}
