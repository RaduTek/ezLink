using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ezLinkSetup
{
    /// <summary>The /datadir verb: %ALLUSERSPROFILE%\ezLink with modify rights for the Users group.</summary>
    internal static class DataDirSetup
    {
        private const InheritanceFlags Inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        public static void Ensure()
        {
            string dir = SetupLog.DataDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                SetupLog.Info("Created {0}", dir);
            }

            // Well-known SID, so this works on any Windows language.
            SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            DirectoryInfo info = new DirectoryInfo(dir);
            DirectorySecurity security = info.GetAccessControl(AccessControlSections.Access);

            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType == AccessControlType.Allow
                    && users.Equals(rule.IdentityReference)
                    && (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify
                    && (rule.InheritanceFlags & Inherit) == Inherit)
                {
                    SetupLog.Info("{0} already grants Users modify rights; unchanged", dir);
                    return;
                }
            }

            security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Modify, Inherit,
                PropagationFlags.None, AccessControlType.Allow));
            info.SetAccessControl(security);
            SetupLog.Info("Granted Users modify rights on {0}", dir);
        }

        /// <summary>Removes the trust store and other data files (only for /uninstall /purge); setup.log stays.</summary>
        public static void Purge()
        {
            string dir = SetupLog.DataDirectory;
            if (!Directory.Exists(dir))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(dir))
            {
                if (!string.Equals(file, SetupLog.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    SetupLog.Info("Deleted {0}", file);
                }
            }
        }
    }
}
