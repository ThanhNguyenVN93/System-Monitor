using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace frm_sys_monitor
{
    // Checks GitHub Releases for a newer build and swaps the running exe in place.
    // Nothing is downloaded or executed without the user's confirmation (see Form1).
    internal static class Updater
    {
        private const string Repo      = "ThanhNguyenVN93/System-Monitor";
        private const string ExeAsset  = "SystemMonitor.exe";
        private const string ShaAsset  = "SystemMonitor.exe.sha256";

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
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
            }
            return ReleaseLogic.Parse(json, ExeAsset, ShaAsset);
        }

        public static bool IsNewer(ReleaseInfo r) => ReleaseLogic.IsNewer(CurrentVersion, r.Version);

        // Downloads into the admin-only runtime directory and verifies the published SHA-256.
        // It must not go next to the exe: that folder may be user-writable (Desktop, Downloads),
        // and a non-admin process could swap the verified file before the elevated swap script
        // moves it into place.
        // Note: the checksum comes from the same release, so it guards against corrupted or
        // truncated downloads, not against a compromised GitHub account (the exe is unsigned).
        // Returns the path of the verified file; throws on any failure (file is deleted).
        public static string Download(ReleaseInfo r)
        {
            string target = Path.Combine(RuntimeDir.Ensure(), "update.exe.new");
            try
            {
                using (var wc = CreateClient())
                {
                    string expected = ReleaseLogic.ExtractSha256(wc.DownloadString(r.ShaUrl));
                    if (expected == null) throw new InvalidDataException("Checksum file is invalid.");

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
        // The script runs elevated, so it lives in the admin-only runtime directory, not %TEMP%,
        // which the non-elevated user could write to between creation and execution.
        public static void ApplyAndRestart(string newFile)
        {
            string exe = ReleaseLogic.EscapeForBatch(System.Windows.Forms.Application.ExecutablePath);
            newFile    = ReleaseLogic.EscapeForBatch(newFile);
            string bat = Path.Combine(RuntimeDir.Ensure(), "update_" + Guid.NewGuid().ToString("N") + ".cmd");

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
