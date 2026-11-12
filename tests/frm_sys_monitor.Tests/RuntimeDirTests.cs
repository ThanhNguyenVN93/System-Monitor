using System;
using System.IO;
using Xunit;

namespace frm_sys_monitor.Tests
{
    public class RuntimeDirTests
    {
        [Fact]
        public void UserTempFolder_IsWritableByNonAdmins()
        {
            // The current user owns %TEMP% with full control.
            Assert.True(RuntimeDir.IsWritableByNonAdmins(Path.GetTempPath()));
        }

        [Fact]
        public void WindowsFolder_IsNotWritableByNonAdmins()
        {
            Assert.False(RuntimeDir.IsWritableByNonAdmins(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        }

        [Fact]
        public void RuntimeDir_IsUnderProgramData()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            Assert.Equal(Path.Combine(programData, "SystemMonitor"), RuntimeDir.FullPath);
        }
    }
}
