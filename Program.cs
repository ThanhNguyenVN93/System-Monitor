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
                ResolveEmbedded(new AssemblyName(args.Name).Name);

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
            "EnergyLib64.dll", "EnergyDriver.sys", "EnergyDriver.inf", "hw_error.log",
        };

        // Assemblies loaded from bytes live outside the normal load context, so the runtime asks
        // again for every assembly that references them. Without a cache each request would load
        // another copy (System.Memory twice → type mismatches); the lock covers the UI thread and
        // the sensor thread resolving at the same time.
        private static readonly System.Collections.Generic.Dictionary<string, Assembly> Resolved =
            new System.Collections.Generic.Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static Assembly ResolveEmbedded(string name)
        {
            lock (Resolved)
            {
                if (Resolved.TryGetValue(name, out var cached)) return cached;

                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("lib." + name + ".dll"))
                {
                    if (stream == null) return null;
                    byte[] data;
                    using (var ms = new MemoryStream()) { stream.CopyTo(ms); data = ms.ToArray(); }

                    Assembly loaded = null;
                    // LHM must be on disk so it can find its embedded kernel driver
                    // (the driver extractor uses Assembly.Location to build the file path).
                    // It goes to the admin-only runtime dir, never next to the exe.
                    if (name == "LibreHardwareMonitorLib")
                    {
                        try { loaded = Assembly.LoadFrom(RuntimeDir.WriteFile(name + ".dll", data)); }
                        catch { /* runtime dir unavailable: load from memory, sensors may be limited */ }
                    }

                    loaded = loaded ?? Assembly.Load(data);
                    Resolved[name] = loaded;
                    return loaded;
                }
            }
        }

        private static void RemoveStaleCopiesNextToExe()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var file in OwnStaleFiles)
                try { File.Delete(Path.Combine(dir, file)); } catch { }
        }
    }
}
