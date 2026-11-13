using System;
using System.Collections;
using System.IO;
using System.Reflection;
using EzLink;

namespace ezLinkSetup
{
    /// <summary>
    /// The /firewall verb: an inbound allow rule for ezLink.exe only, limited to the link-local range
    /// 169.254.0.0/16. XP SP2/SP3 uses the HNetCfg.FwMgr authorized-application list; Vista and later use
    /// HNetCfg.FwPolicy2 with one TCP and one UDP rule. The COM objects are driven by late binding so no
    /// interop assembly is needed. Restricting the Vista rules to the link interface happens once the link
    /// adapter is known (a later step), so it is not set here.
    /// </summary>
    internal static class FirewallSetup
    {
        private const string RemoteAddresses = "169.254.0.0/255.255.0.0"; // 169.254.0.0/16
        private const string Description = "Lets the other computer reach ezLink over the ezLink cable.";
        private const string TcpRuleName = "ezLink (TCP-In)";
        private const string UdpRuleName = "ezLink (UDP-In)";
        private const string XpName = "ezLink";

        // Constants from netfw.h
        private const int ProfilesAll = 0x7FFFFFFF;  // NET_FW_PROFILE2_ALL
        private const int DirectionIn = 1;           // NET_FW_RULE_DIR_IN
        private const int ActionAllow = 1;           // NET_FW_ACTION_ALLOW
        private const int ProtocolTcp = 6;
        private const int ProtocolUdp = 17;
        private const int ScopeCustom = 2;           // NET_FW_SCOPE_CUSTOM
        private const int IpVersionAny = 2;          // NET_FW_IP_VERSION_ANY

        private static bool IsVistaOrLater
        {
            get { return Environment.OSVersion.Version.Major >= 6; }
        }

        private static string ExePath
        {
            get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ezLink.exe")); }
        }

        public static void Install()
        {
            string exe = ExePath;
            if (!File.Exists(exe))
            {
                throw new FileNotFoundException("ezLink.exe was not found next to the setup helper", exe);
            }

            if (IsVistaOrLater)
            {
                EnsureRule(TcpRuleName, exe, ProtocolTcp, Constants.SessionPort);
                EnsureRule(UdpRuleName, exe, ProtocolUdp, Constants.DiscoveryPort);
            }
            else
            {
                EnsureXpApplication(exe);
            }
        }

        public static void Uninstall()
        {
            if (IsVistaOrLater)
            {
                RemoveRules(TcpRuleName);
                RemoveRules(UdpRuleName);
                return;
            }

            string exe = ExePath;
            foreach (object profile in XpProfiles())
            {
                object apps = Get(profile, "AuthorizedApplications");
                if (FindXpApplication(apps, exe) != null)
                {
                    Call(apps, "Remove", exe);
                    SetupLog.Info("Removed the firewall exception for {0}", exe);
                }
                else
                {
                    SetupLog.Info("No firewall exception for {0} in this profile; unchanged", exe);
                }
            }
        }

        // ---- Vista and later -------------------------------------------------------------------------------

        private static object CreateCom(string progId)
        {
            Type type = Type.GetTypeFromProgID(progId);
            if (type == null)
            {
                throw new InvalidOperationException("The Windows Firewall interface " + progId + " is not available");
            }
            return Activator.CreateInstance(type);
        }

        private static void EnsureRule(string name, string exe, int protocol, int port)
        {
            object policy = CreateCom("HNetCfg.FwPolicy2");
            object rules = Get(policy, "Rules");
            object rule = FindRule(rules, name);
            bool created = rule == null;
            if (created)
            {
                rule = CreateCom("HNetCfg.FWRule");
                Set(rule, "Name", name);
            }

            bool changed = false;
            changed |= SetIfDifferent(rule, "Description", Description);
            changed |= SetIfDifferent(rule, "ApplicationName", exe);
            changed |= SetIfDifferent(rule, "Protocol", protocol);
            changed |= SetIfDifferent(rule, "LocalPorts", port.ToString());
            changed |= SetIfDifferent(rule, "RemoteAddresses", RemoteAddresses);
            changed |= SetIfDifferent(rule, "Direction", DirectionIn);
            changed |= SetIfDifferent(rule, "Action", ActionAllow);
            changed |= SetIfDifferent(rule, "Profiles", ProfilesAll);
            changed |= SetIfDifferent(rule, "Enabled", true);

            if (created)
            {
                Call(rules, "Add", rule);
                SetupLog.Info("Firewall rule \"{0}\" added", name);
            }
            else if (changed)
            {
                SetupLog.Info("Firewall rule \"{0}\" updated", name);
            }
            else
            {
                SetupLog.Info("Firewall rule \"{0}\" is up to date; unchanged", name);
            }
        }

        private static object FindRule(object rules, string name)
        {
            foreach (object rule in (IEnumerable)rules)
            {
                if (string.Equals((string)Get(rule, "Name"), name, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }
            }
            return null;
        }

        private static void RemoveRules(string name)
        {
            object rules = Get(CreateCom("HNetCfg.FwPolicy2"), "Rules");
            bool removed = false;
            // Remove drops one rule per call, and a stale duplicate could exist; stop after a sane number.
            for (int i = 0; i < 10 && FindRule(rules, name) != null; i++)
            {
                Call(rules, "Remove", name);
                removed = true;
            }
            SetupLog.Info(removed ? "Removed firewall rule \"{0}\"" : "Firewall rule \"{0}\" not present; unchanged", name);
        }

        // ---- XP SP2/SP3 -------------------------------------------------------------------------------------

        /// <summary>The domain (0) and standard (1) profiles, so the exception holds whichever is active.</summary>
        private static object[] XpProfiles()
        {
            object manager = CreateCom("HNetCfg.FwMgr");
            object policy = Get(manager, "LocalPolicy");
            return new object[] { Call(policy, "GetProfileByType", 0), Call(policy, "GetProfileByType", 1) };
        }

        private static object FindXpApplication(object apps, string exe)
        {
            foreach (object app in (IEnumerable)apps)
            {
                if (string.Equals((string)Get(app, "ProcessImageFileName"), exe, StringComparison.OrdinalIgnoreCase))
                {
                    return app;
                }
            }
            return null;
        }

        private static void EnsureXpApplication(string exe)
        {
            foreach (object profile in XpProfiles())
            {
                object apps = Get(profile, "AuthorizedApplications");
                object app = FindXpApplication(apps, exe);
                bool created = app == null;
                if (created)
                {
                    app = CreateCom("HNetCfg.FwAuthorizedApplication");
                    Set(app, "ProcessImageFileName", exe);
                }

                bool changed = false;
                changed |= SetIfDifferent(app, "Name", XpName);
                changed |= SetIfDifferent(app, "IpVersion", IpVersionAny);
                changed |= SetIfDifferent(app, "Scope", ScopeCustom);
                changed |= SetIfDifferent(app, "RemoteAddresses", RemoteAddresses);
                changed |= SetIfDifferent(app, "Enabled", true);

                if (created)
                {
                    Call(apps, "Add", app);
                    SetupLog.Info("Firewall exception for {0} added", exe);
                }
                else if (changed)
                {
                    SetupLog.Info("Firewall exception for {0} updated", exe);
                }
                else
                {
                    SetupLog.Info("Firewall exception for {0} is up to date; unchanged", exe);
                }
            }
        }

        // ---- late-bound COM helpers ---------------------------------------------------------------------------

        private static object Get(object target, string property)
        {
            return Invoke(target, property, BindingFlags.GetProperty, null);
        }

        private static void Set(object target, string property, object value)
        {
            Invoke(target, property, BindingFlags.SetProperty, new object[] { value });
        }

        private static object Call(object target, string method, params object[] args)
        {
            return Invoke(target, method, BindingFlags.InvokeMethod, args);
        }

        private static bool SetIfDifferent(object target, string property, object value)
        {
            object current = Get(target, property);
            if (current != null && string.Equals(Convert.ToString(current), Convert.ToString(value), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            Set(target, property, value);
            return true;
        }

        private static object Invoke(object target, string member, BindingFlags flags, object[] args)
        {
            try
            {
                return target.GetType().InvokeMember(member, flags, null, target, args);
            }
            catch (TargetInvocationException ex)
            {
                // Surface the COM error itself rather than the reflection wrapper.
                throw new InvalidOperationException("Windows Firewall: " + member + " failed: " + ex.InnerException.Message, ex.InnerException);
            }
        }
    }
}
