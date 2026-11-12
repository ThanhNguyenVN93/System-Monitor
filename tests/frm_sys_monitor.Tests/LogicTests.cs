using System;
using Xunit;

namespace frm_sys_monitor.Tests
{
    public class CpuTempPickerTests
    {
        [Fact]
        public void NoOffers_HasNoValue()
        {
            Assert.Null(new CpuTempPicker().Value);
        }

        [Fact]
        public void BetterRankedSensorWins_EvenWhenCooler()
        {
            var p = new CpuTempPicker();
            p.Offer("Core Average", 70);
            p.Offer("CPU Package", 55);
            p.Offer("Core (Max)", 80);
            Assert.Equal(55, p.Value);
        }

        [Fact]
        public void SameRank_HotterWins()
        {
            var p = new CpuTempPicker();
            p.Offer("CPU", 40);
            p.Offer("cpu", 48);   // names compare case-insensitively
            p.Offer("CPU", 45);
            Assert.Equal(48, p.Value);
        }

        [Fact]
        public void UnknownName_IsUsedOnlyWithoutBetterSensor()
        {
            var p = new CpuTempPicker();
            p.Offer("CPU Core #1", 60);
            Assert.Equal(60, p.Value);
            p.Offer("Temperature", 50);
            Assert.Equal(50, p.Value);
        }

        [Fact]
        public void Rank_UnknownIsLast()
        {
            Assert.Equal(0, CpuTempPicker.Rank("CPU Package"));
            Assert.Equal(CpuTempPicker.Priority.Length, CpuTempPicker.Rank("Something"));
        }
    }

    public class ReleaseLogicTests
    {
        private const string Exe = "SystemMonitor.exe";
        private const string Sha = "SystemMonitor.exe.sha256";

        private static string Json(string tag, bool draft = false, params string[] assets)
        {
            var items = new System.Text.StringBuilder();
            foreach (var a in assets)
            {
                if (items.Length > 0) items.Append(',');
                items.Append("{\"name\":\"" + a + "\",\"size\":1,\"browser_download_url\":\"https://example.test/dl/" + a + "\"}");
            }
            return "{\"tag_name\":\"" + tag + "\",\"name\":\"Release\",\"draft\":" + (draft ? "true" : "false") +
                   ",\"body\":\"notes with \\\"quotes\\\" and tag_name inside\",\"assets\":[" + items + "]}";
        }

        [Fact]
        public void Parse_FindsVersionAndAssets()
        {
            var r = ReleaseLogic.Parse(Json("v1.2.3", false, Exe, Sha, "other.zip"), Exe, Sha);
            Assert.NotNull(r);
            Assert.Equal(new Version(1, 2, 3), r.Version);
            Assert.Equal("v1.2.3", r.Tag);
            Assert.EndsWith("/" + Exe, r.ExeUrl);
            Assert.EndsWith("/" + Sha, r.ShaUrl);
        }

        [Fact]
        public void Parse_MissingChecksum_ReturnsNull()
        {
            Assert.Null(ReleaseLogic.Parse(Json("v1.2.3", false, Exe), Exe, Sha));
        }

        [Fact]
        public void Parse_DraftOrBadTag_ReturnsNull()
        {
            Assert.Null(ReleaseLogic.Parse(Json("v1.2.3", true, Exe, Sha), Exe, Sha));
            Assert.Null(ReleaseLogic.Parse(Json("nightly", false, Exe, Sha), Exe, Sha));
        }

        [Fact]
        public void Parse_GarbageJson_ReturnsNull()
        {
            Assert.Null(ReleaseLogic.Parse("<html>rate limited</html>", Exe, Sha));
        }

        [Theory]
        [InlineData("1.1.0.0", "1.1.1",   true)]
        [InlineData("1.1.0.0", "1.2",     true)]
        [InlineData("1.1.0.0", "1.1.0",   false)]
        [InlineData("1.1.0.0", "1.1.0.5", false)]   // 4th component ignored
        [InlineData("1.2.0.0", "1.1.9",   false)]
        public void IsNewer(string current, string latest, bool expected)
        {
            Assert.Equal(expected, ReleaseLogic.IsNewer(Version.Parse(current), Version.Parse(latest)));
        }

        [Fact]
        public void ExtractSha256_BareAndSha256sumFormat()
        {
            string h = new string('a', 32) + new string('B', 32);
            Assert.Equal(h, ReleaseLogic.ExtractSha256(h + "\n"));
            Assert.Equal(h, ReleaseLogic.ExtractSha256(h + "  SystemMonitor.exe"));
            Assert.Null(ReleaseLogic.ExtractSha256("not a hash"));
            Assert.Null(ReleaseLogic.ExtractSha256(h + "c"));   // 65 hex chars is not a SHA-256
        }

        [Fact]
        public void EscapeForBatch_DoublesPercent()
        {
            Assert.Equal(@"C:\100%% sure\a&b^c.exe", ReleaseLogic.EscapeForBatch(@"C:\100% sure\a&b^c.exe"));
        }
    }
}
