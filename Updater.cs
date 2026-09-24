using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace frm_sys_monitor
{
    // Checks GitHub Releases for a newer build and swaps the running exe in place.
    // Nothing is downloaded or executed without the user's confirmation (see Form1).
    internal static class Updater
    {
        private const string Repo      = "ThanhNguyenVN93/System-Monitor";
        private const string ExeAsset  = "SystemMonitor.exe";
        private const string ShaAsset  = "SystemMonitor.exe.sha256";

        internal class ReleaseInfo
        {
            public Version Version;
            public string  Tag;
            public string  ExeUrl;
            public string  ShaUrl;
        }

        public static Version CurrentVersion =>
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        private static WebClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "SystemMonitor-Updater";
            return wc;
        }

        // Returns null when the latest release has no usable exe + checksum asset.
        public static ReleaseInfo CheckLatest()
        {
            string json;
            using (var wc = CreateClient())
                json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!tag.Success) return null;
            if (!Version.TryParse(tag.Groups[1].Value.TrimStart('v', 'V'), out Version ver)) return null;

            var info = new ReleaseInfo { Version = ver, Tag = tag.Groups[1].Value };
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                string url = m.Groups[1].Value;
                if (url.EndsWith("/" + ExeAsset, StringComparison.OrdinalIgnoreCase)) info.ExeUrl = url;
                else if (url.EndsWith("/" + ShaAsset, StringComparison.OrdinalIgnoreCase)) info.ShaUrl = url;
            }
            return info.ExeUrl != null && info.ShaUrl != null ? info : null;
        }

        public static bool IsNewer(ReleaseInfo r)
        {
            // Compare only major.minor.build so a 4th component never causes a phantom update.
            var cur = CurrentVersion;
            var a = new Version(cur.Major, cur.Minor, Math.Max(cur.Build, 0));
            var b = new Version(r.Version.Major, r.Version.Minor, Math.Max(r.Version.Build, 0));
            return b > a;
        }

        // Downloads next to the running exe and verifies the published SHA-256.
        // Returns the path of the verified file; throws on any failure (file is deleted).
        public static string Download(ReleaseInfo r)
        {
            string target = System.Windows.Forms.Application.ExecutablePath + ".new";
            try
            {
                using (var wc = CreateClient())
                {
                    string expected = Regex.Match(wc.DownloadString(r.ShaUrl), "[0-9a-fA-F]{64}").Value;
                    if (expected.Length != 64) throw new InvalidDataException("Checksum file is invalid.");

                    wc.DownloadFile(r.ExeUrl, target);

                    using (var fs = File.OpenRead(target))
                    using (var sha = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
                        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Downloaded file failed the SHA-256 check.");
                    }
                }
                return target;
            }
            catch
            {
                try { File.Delete(target); } catch { }
                throw;
            }
        }

        // The running exe is locked, so a small script waits for this process to exit,
        // moves the new file over it and relaunches. The caller must exit right after.
        public static void ApplyAndRestart(string newFile)
        {
            string exe = System.Windows.Forms.Application.ExecutablePath;
            string bat = Path.Combine(Path.GetTempPath(), "sysmon_update_" + Guid.NewGuid().ToString("N") + ".cmd");

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("set n=0");
            sb.AppendLine(":wait");
            sb.AppendLine("set /a n+=1");
            sb.AppendLine("if %n% gtr 30 goto fail");
            sb.AppendLine("move /y \"" + newFile + "\" \"" + exe + "\" >nul 2>&1");
            sb.AppendLine("if errorlevel 1 (ping 127.0.0.1 -n 2 >nul & goto wait)");
            sb.AppendLine("start \"\" \"" + exe + "\"");
            sb.AppendLine("(goto) 2>nul & del \"%~f0\"");
            sb.AppendLine(":fail");
            sb.AppendLine("del \"" + newFile + "\" >nul 2>&1");
            sb.AppendLine("(goto) 2>nul & del \"%~f0\"");
            File.WriteAllText(bat, sb.ToString(), Encoding.Default);

            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"")
            {
                CreateNoWindow  = true,
                UseShellExecute = false,
                WindowStyle     = ProcessWindowStyle.Hidden,
            });
        }
    }
}
