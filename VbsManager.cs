using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace frm_sys_monitor
{
    internal static class VbsManager
    {
        public static bool IsSecureBootOn { get; private set; }
        public static bool IsVbsOn        { get; private set; }
        public static bool IsHvciOn       { get; private set; }

        /// <summary>True when any flag blocks LHM CPU sensors.</summary>
        public static bool IsSensorBlocked => IsVbsOn || IsHvciOn;

        public static void Detect()
        {
            IsSecureBootOn = CheckSecureBoot();
            IsVbsOn        = CheckVbs();
            IsHvciOn       = CheckHvci();
        }

        // ── Registry checks ───────────────────────────────────────────────
        private static bool CheckSecureBoot()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                    return key != null && key.GetValue("UEFISecureBootEnabled") is int v && v == 1;
            }
            catch { return false; }
        }

        // Registry only says "configured"; Win11 can run VBS with the key at 0/missing.
        // The WMI status (2 = running) reflects what is actually active right now.
        private static bool CheckVbs()
        {
            if (QueryDeviceGuard(out int status, out _)) return status == 2;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\DeviceGuard"))
                    return key != null && key.GetValue("EnableVirtualizationBasedSecurity") is int v && v == 1;
            }
            catch { return false; }
        }

        private static bool QueryDeviceGuard(out int vbsStatus, out int[] servicesRunning)
        {
            vbsStatus = 0; servicesRunning = new int[0];
            try
            {
                using (var q = new System.Management.ManagementObjectSearcher(
                    @"root\Microsoft\Windows\DeviceGuard", "SELECT * FROM Win32_DeviceGuard"))
                foreach (System.Management.ManagementObject o in q.Get())
                {
                    vbsStatus = Convert.ToInt32(o["VirtualizationBasedSecurityStatus"]);
                    if (o["SecurityServicesRunning"] is ushort[] svc)
                        servicesRunning = Array.ConvertAll(svc, x => (int)x);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool CheckHvci()
        {
            if (QueryDeviceGuard(out _, out int[] running) && Array.IndexOf(running, 2) >= 0) return true;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"))
                    return key != null && key.GetValue("Enabled") is int v && v == 1;
            }
            catch { return false; }
        }

        // ── Fix ───────────────────────────────────────────────────────────
        /// <summary>Disables VBS, HVCI and Hyper-V. Returns false + reason on failure.</summary>
        public static bool DisableAll(out string error)
        {
            error = null;
            try
            {
                // Disable VBS
                RunReg(@"add ""HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard"" /v EnableVirtualizationBasedSecurity /t REG_DWORD /d 0 /f");
                // Disable HVCI
                RunReg(@"add ""HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"" /v Enabled /t REG_DWORD /d 0 /f");
                // Disable Hyper-V boot
                RunBcdedit("hypervisorlaunchtype off");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void RunReg(string args)
        {
            var psi = new ProcessStartInfo("reg", args)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using (var p = Process.Start(psi)) p.WaitForExit();
        }

        private static void RunBcdedit(string args)
        {
            var psi = new ProcessStartInfo("bcdedit", args)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            using (var p = Process.Start(psi)) p.WaitForExit();
        }
    }
}
