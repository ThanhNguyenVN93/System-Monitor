using System;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace frm_sys_monitor
{
    // Thin wrapper over the Service Control Manager for the kernel drivers this app uses.
    internal static class KernelService
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string machine, string db, uint access);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr hSCM, string name, uint access);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateService(IntPtr hSCM, string name, string displayName,
            uint access, uint type, uint start, uint error, string binary,
            string group, IntPtr tagId, string deps, string account, string password);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig(IntPtr hSvc, uint type, uint start, uint error,
            string binary, string group, IntPtr tagId, string deps, string account, string password, string displayName);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool StartService(IntPtr hSvc, uint numArgs, string[] args);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DeleteService(IntPtr hSvc);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr h);

        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SC_MANAGER_CONNECT    = 0x0001;
        private const uint SERVICE_ALL_ACCESS    = 0xF01FF;
        private const uint SERVICE_STOP          = 0x0020;
        private const uint DELETE                = 0x00010000;
        private const uint SERVICE_KERNEL_DRIVER = 0x00000001;
        private const uint SERVICE_DEMAND_START  = 0x00000003;
        private const uint SERVICE_ERROR_NORMAL  = 0x00000001;
        private const uint SERVICE_NO_CHANGE     = 0xFFFFFFFF;

        public static bool IsRunning(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    return sc.Status == ServiceControllerStatus.Running;
            }
            catch { return false; }   // not installed
        }

        // Creates the service if missing and starts it. An existing service is only re-pointed
        // when it still uses a path from an older version of this app (next to the exe); a driver
        // installed by its own installer is left alone. Returns true when the service was created here.
        public static bool InstallAndStart(string name, string sysPath)
        {
            bool created = false;
            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (scm == IntPtr.Zero) return false;
            try
            {
                IntPtr svc = OpenService(scm, name, SERVICE_ALL_ACCESS);
                if (svc == IntPtr.Zero)
                {
                    svc = CreateService(scm, name, name,
                        SERVICE_ALL_ACCESS, SERVICE_KERNEL_DRIVER, SERVICE_DEMAND_START,
                        SERVICE_ERROR_NORMAL, sysPath, null, IntPtr.Zero, null, null, null);
                    created = svc != IntPtr.Zero;
                }
                else if (ImageIsIn(name, AppDomain.CurrentDomain.BaseDirectory))
                {
                    ChangeServiceConfig(svc, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE,
                        sysPath, null, IntPtr.Zero, null, null, null, null);
                }
                if (svc == IntPtr.Zero) return false;
                try { StartService(svc, 0, null); }
                finally { CloseServiceHandle(svc); }
            }
            finally { CloseServiceHandle(scm); }
            return created;
        }

        public static void StopAndDelete(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                    if (sc.Status != ServiceControllerStatus.Stopped) sc.Stop();
            }
            catch { }

            IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero) return;
            try
            {
                IntPtr svc = OpenService(scm, name, SERVICE_STOP | DELETE);
                if (svc == IntPtr.Zero) return;
                DeleteService(svc);
                CloseServiceHandle(svc);
            }
            finally { CloseServiceHandle(scm); }
        }

        // True when the service's driver file sits directly in the given directory.
        public static bool ImageIsIn(string name, string directory)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
                {
                    string image = key?.GetValue("ImagePath") as string;
                    if (string.IsNullOrEmpty(image)) return false;
                    if (image.StartsWith(@"\??\")) image = image.Substring(4);
                    string dir = Path.GetDirectoryName(image.Trim('"'));
                    string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                    return string.Equals(dir?.TrimEnd('\\'), appDir, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }
    }

    internal static class PawnIoInstaller
    {
        private const string ServiceName = "PawnIo";

        // Extracts pawnio.sys into the protected runtime directory and installs the service.
        // Must be called before Computer.Open() so LHM can use MSR sensors.
        // PawnIO is shared with other tools (LHM, FanControl, ...), so it is never removed on exit.
        public static void EnsureInstalled()
        {
            if (KernelService.IsRunning(ServiceName)) return;

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            string sysPath;
            using (var s = asm.GetManifestResourceStream("pawnio.pawnio.sys"))
            {
                if (s == null) return;
                sysPath = RuntimeDir.WriteFile("pawnio.sys", s);
            }
            KernelService.InstallAndStart(ServiceName, sysPath);
        }
    }

    internal static class IntelPowerApi
    {
        private const string ServiceName = "EnergyDriver";

        [DllImport("EnergyLib64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool IntelEnergyLibInitialize();

        [DllImport("EnergyLib64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool GetPowerData(int packageIndex, int domainIndex,
            out double powerW, out double energyJ, out double timeS);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr hFile, uint flags);
        private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        /// <summary>True when this process created the EnergyDriver service (so it may remove it on exit).</summary>
        public static bool CreatedService { get; private set; }

        // Extracts embedded files, installs driver if needed, then initializes the API.
        public static bool TryInitialize()
        {
            try
            {
                if (!ExtractEmbedded()) return false;
                EnsureDriverRunning();

                // Load by full path from the protected directory (and resolve its VC++ runtime
                // from there too) before the first P/Invoke binds to "EnergyLib64.dll" by name.
                string dll = Path.Combine(RuntimeDir.FullPath, "EnergyLib64.dll");
                if (LoadLibraryEx(dll, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH) == IntPtr.Zero) return false;
                return IntelEnergyLibInitialize();
            }
            catch { return false; }
        }

        // Extracts all resources with "intel." prefix. Returns false when the optional files aren't bundled.
        private static bool ExtractEmbedded()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            bool any = false;
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith("intel.")) continue;
                using (var s = asm.GetManifestResourceStream(res))
                    RuntimeDir.WriteFile(res.Substring("intel.".Length), s);
                any = true;
            }
            return any;
        }

        private static void EnsureDriverRunning()
        {
            if (KernelService.IsRunning(ServiceName)) return;
            var sysPath = Path.Combine(RuntimeDir.FullPath, "EnergyDriver.sys");
            if (!File.Exists(sysPath)) return;
            if (KernelService.InstallAndStart(ServiceName, sysPath)) CreatedService = true;
        }

        // Removes EnergyDriver only if it is ours: created this session, or left behind by an
        // earlier run (its driver file is in our runtime / app directory).
        public static void Cleanup()
        {
            if (CreatedService
                || KernelService.ImageIsIn(ServiceName, RuntimeDir.FullPath)
                || KernelService.ImageIsIn(ServiceName, AppDomain.CurrentDomain.BaseDirectory))
                KernelService.StopAndDelete(ServiceName);
        }
    }
}
