using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace frm_sys_monitor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
#if !DEBUG
            RemoveStaleCopiesNextToExe();
#endif
            // Load bundled DLLs from embedded resources when not found on disk.
            // This makes the exe fully self-contained for distribution.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("lib." + name + ".dll"))
                {
                    if (stream == null) return null;
                    var data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);

                    // LHM must be on disk so it can find its embedded kernel driver
                    // (the driver extractor uses Assembly.Location to build the file path).
                    // It goes to the admin-only runtime dir, never next to the exe.
                    if (name == "LibreHardwareMonitorLib")
                    {
                        try { return Assembly.LoadFrom(RuntimeDir.WriteFile(name + ".dll", data)); }
                        catch { /* runtime dir unavailable: load from memory, sensors may be limited */ }
                    }

                    return Assembly.Load(data);
                }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        // Older versions wrote these next to the exe; the CLR / LoadLibrary look there first, so a
        // copy in a user-writable folder could be swapped and then loaded by this elevated process.
        // Only names unique to this app are removed: generic ones (System.*.dll, VC++ runtime)
        // may belong to another program sharing the folder.
        private static readonly string[] OwnStaleFiles =
        {
            "LibreHardwareMonitorLib.dll", "pawnio.sys", "pawnio.inf",
            "EnergyLib64.dll", "EnergyDriver.sys", "EnergyDriver.inf",
        };

        private static void RemoveStaleCopiesNextToExe()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var file in OwnStaleFiles)
                try { File.Delete(Path.Combine(dir, file)); } catch { }
        }
    }
}
