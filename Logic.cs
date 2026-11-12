using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

// Pure logic with no WinForms / LibreHardwareMonitor dependency, so the test project can
// compile this file directly.
namespace frm_sys_monitor
{
    // Picks the most meaningful CPU temperature among the sensors LHM exposes.
    internal sealed class CpuTempPicker
    {
        public static readonly string[] Priority =
        {
            "CPU Package",          // Intel/AMD package sensor (most accurate)
            "Core (Max)",           // Intel Gen 12+ LHM naming (has parentheses)
            "Core Max",             // alternate naming without parentheses
            "Core Average",
            "CPU Tdie", "Tctl/Tdie", "CPU Die", "CPU", "Temperature"
        };

        private int _rank = int.MaxValue;
        public int? Value { get; private set; }

        public static int Rank(string name)
        {
            for (int i = 0; i < Priority.Length; i++)
                if (string.Equals(Priority[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return Priority.Length;   // unknown names rank last, but still count
        }

        // Better-ranked name wins; on equal rank the hotter reading wins.
        public void Offer(string name, int temp)
        {
            int rank = Rank(name);
            if (Value == null || rank < _rank || (rank == _rank && temp > Value.Value))
            {
                _rank = rank;
                Value = temp;
            }
        }
    }

    internal class ReleaseInfo
    {
        public Version Version;
        public string  Tag;
        public string  ExeUrl;
        public string  ShaUrl;
    }

    internal static class ReleaseLogic
    {
#pragma warning disable CS0649   // DTO fields are set by the JSON serializer
        [DataContract]
        private class GhRelease
        {
            [DataMember(Name = "tag_name")] public string TagName;
            [DataMember(Name = "draft")]    public bool Draft;
            [DataMember(Name = "assets")]   public GhAsset[] Assets;
        }

        [DataContract]
        private class GhAsset
        {
            [DataMember(Name = "name")]                 public string Name;
            [DataMember(Name = "browser_download_url")] public string Url;
        }
#pragma warning restore CS0649

        // Parses a GitHub "releases/latest" response. Returns null when the release has no
        // parseable version or lacks either asset.
        public static ReleaseInfo Parse(string json, string exeAsset, string shaAsset)
        {
            GhRelease rel;
            try
            {
                var ser = new DataContractJsonSerializer(typeof(GhRelease));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    rel = (GhRelease)ser.ReadObject(ms);
            }
            catch (SerializationException) { return null; }

            if (rel?.TagName == null || rel.Draft) return null;
            if (!Version.TryParse(rel.TagName.TrimStart('v', 'V'), out Version ver)) return null;

            var info = new ReleaseInfo { Version = ver, Tag = rel.TagName };
            foreach (var a in rel.Assets ?? new GhAsset[0])
            {
                if (string.Equals(a.Name, exeAsset, StringComparison.OrdinalIgnoreCase)) info.ExeUrl = a.Url;
                else if (string.Equals(a.Name, shaAsset, StringComparison.OrdinalIgnoreCase)) info.ShaUrl = a.Url;
            }
            return info.ExeUrl != null && info.ShaUrl != null ? info : null;
        }

        // Compares only major.minor.build so a 4th component never causes a phantom update.
        public static bool IsNewer(Version current, Version latest)
        {
            var a = new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
            var b = new Version(latest.Major,  latest.Minor,  Math.Max(latest.Build,  0));
            return b > a;
        }

        // Accepts both a bare hash and "sha256sum" style "<hash>  file" content.
        public static string ExtractSha256(string text)
        {
            var m = Regex.Match(text ?? "", @"\b[0-9a-fA-F]{64}\b");
            return m.Success ? m.Value : null;
        }

        // Paths go inside double quotes in a .cmd file: only '%' still needs escaping there.
        public static string EscapeForBatch(string path) => path.Replace("%", "%%");
    }
}
