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
                    if (name == "LibreHardwareMonitorLib")
                    {
                        string diskPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory, name + ".dll");
                        if (!File.Exists(diskPath))
                            File.WriteAllBytes(diskPath, data);
                        return Assembly.LoadFrom(diskPath);
                    }

                    return Assembly.Load(data);
                }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
