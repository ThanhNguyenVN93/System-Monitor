using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace frm_sys_monitor
{
    // Holds every file the app extracts and then loads with admin rights (LHM, PawnIO driver,
    // optional Intel files). The exe itself may live somewhere a normal user can write to
    // (Downloads, Desktop), so those files must not go next to it: a non-admin could swap them
    // and the elevated app would load them. %ProgramData%\SystemMonitor gets an ACL where only
    // Administrators / SYSTEM can write.
    internal static class RuntimeDir
    {
        public static readonly string FullPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SystemMonitor");

        private static readonly object Sync = new object();
        private static bool _ready;

        private static readonly SecurityIdentifier Admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier SystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier Users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        // Creates (or repairs) the directory. Throws when it can't be made safe; callers then skip
        // the feature that needed it instead of falling back to an unsafe location.
        public static string Ensure()
        {
            lock (Sync)
            {
                if (_ready) return FullPath;

                var di = new DirectoryInfo(FullPath);
                if (di.Exists && !IsTrusted(di))
                {
                    // Pre-created by someone else, or a junction: move it aside without following it.
                    Directory.Move(FullPath, FullPath + ".untrusted." + Guid.NewGuid().ToString("N"));
                }

                if (!Directory.Exists(FullPath)) Directory.CreateDirectory(FullPath, BuildSecurity());
                else Directory.SetAccessControl(FullPath, BuildSecurity());

                _ready = true;
                return FullPath;
            }
        }

        // Writes the file only when its content differs, so new app versions replace old copies.
        public static string WriteFile(string name, byte[] data)
        {
            string target = Path.Combine(Ensure(), name);
            if (File.Exists(target))
            {
                try { if (File.ReadAllBytes(target).SequenceEqual(data)) return target; }
                catch { }
            }
            File.WriteAllBytes(target, data);
            return target;
        }

        public static string WriteFile(string name, Stream src)
        {
            using (var ms = new MemoryStream())
            {
                src.CopyTo(ms);
                return WriteFile(name, ms.ToArray());
            }
        }

        // True when someone other than Administrators / SYSTEM / TrustedInstaller may write into the
        // folder (e.g. the exe sits in Downloads). Deny rules are ignored, so this can over-report;
        // it only drives a one-time advisory.
        public static bool IsWritableByNonAdmins(string directory)
        {
            const FileSystemRights writeLike = FileSystemRights.WriteData | FileSystemRights.AppendData
                | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
            const string trustedInstaller = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
            try
            {
                var acl = Directory.GetAccessControl(directory, AccessControlSections.Access);
                foreach (FileSystemAccessRule r in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (r.AccessControlType != AccessControlType.Allow) continue;
                    if ((r.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
                    if ((r.FileSystemRights & writeLike) == 0) continue;
                    var sid = (SecurityIdentifier)r.IdentityReference;
                    if (sid == Admins || sid == SystemSid || sid.Value == trustedInstaller) continue;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static bool IsTrusted(DirectoryInfo di)
        {
            try
            {
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                var owner = di.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
                return owner == Admins || owner == SystemSid;
            }
            catch { return false; }
        }

        private static DirectorySecurity BuildSecurity()
        {
            const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            var sec = new DirectorySecurity();
            sec.SetAccessRuleProtection(true, false);   // drop inherited "Users: create files" from ProgramData
            sec.SetOwner(Admins);
            sec.AddAccessRule(new FileSystemAccessRule(Admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(SystemSid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
            return sec;
        }
    }
}
