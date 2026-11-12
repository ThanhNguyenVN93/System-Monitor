using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace frm_sys_monitor
{
    public partial class Form1 : Form
    {
        // ── Win32 ─────────────────────────────────────────────────────────
        private const int WM_NCHITTEST      = 0x84;
        private const int HTCLIENT          = 1;
        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_LAYERED     = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;

        [DllImport("user32.dll")]   private static extern int    GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]   private static extern int    SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]   private static extern bool   DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")]   private static extern int    SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);
        [DllImport("kernel32.dll")] private static extern ulong  GetTickCount64();

        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public int AccentState;    // 4 = ACCENT_ENABLE_ACRYLICBLURBEHIND
            public int AccentFlags;
            public int GradientColor;  // ABGR packed int
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public int    Attribute;   // 19 = WCA_ACCENT_POLICY
            public IntPtr Data;
            public int    SizeOfData;
        }

        // ── Fields ────────────────────────────────────────────────────────
        private Computer   _computer;
        private Timer      _systemTimer;
        private NotifyIcon _trayIcon;
        private ToolTip    _toolTip;
        private bool       _clickThrough;
        private bool       _hvciEnabled;
        private volatile bool _hardwareReady;
        private bool       _dragging;

        private Point      _dragOffset;
        private const int  SnapThreshold = 20;
        private int        _hwTick;
        private IntPtr     _trayIconHandle;


        private Guna.UI2.WinForms.Guna2HtmlLabel _lblSsdTemp;

        private float  _cpuMaxClockMHz;
        private bool   _autoStart;
        private Point? _savedLocation;
        private bool   _autoStartConfigured;
        private bool   _folderWarned;          // "exe folder is user-writable" advisory shown once   // first-run registration done; respect the user's choice after that
        private int    _lastCpuLoad;
        private int    _lastGpuLoad;
        private bool   _intelPowerOk;
        private Color  _accentColor = Color.Cyan;
        private const int SensorFailThreshold = 10;   // hardware ticks (~1 s each) with no CPU temp/power
        private int    _sensorFailStreak;
        private bool   _sensorCheckDone;
        private bool   _cpuSensorsOk;
        private bool   _autoCheckUpdates = true;
        private ReleaseInfo _pendingUpdate;
        private bool   _updateBusy;
        private bool   _balloonIsUpdate;
        private ToolStripMenuItem _updateItem;
        private bool   _suppressVbsPrompt;     // user chose "No" — don't nag on every launch
        private bool   _vbsDisableAttempted;    // we already disabled VBS once; if it's still on, it's Core isolation/policy
        private PerformanceCounter _cpuPerfCounter;

        private static readonly string SettingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.cfg");

        // Dynamic label added when HVCI hides lblPower
        private Guna.UI2.WinForms.Guna2HtmlLabel _lblGpuDetail;

        // ── Constructor ───────────────────────────────────────────────────
        public Form1()
        {
            InitializeComponent();
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= WS_EX_LAYERED; return cp; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST) { m.Result = new IntPtr(HTCLIENT); return; }
            base.WndProc(ref m);
        }

        // ── Load ──────────────────────────────────────────────────────────
        private void Form1_Load(object sender, EventArgs e)
        {
            guna2ShadowForm1.SetShadowForm(this);

            var wa = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(wa.Right - this.Width - 10, wa.Top + 10);

            lblMachineName.Text = $"◈  {Environment.MachineName}";

            LoadSettings();   // must be first — sets _accentColor before tray icon
            RestoreSavedPosition();
            EnableAcrylic();
            InitTray();
            WarnIfFolderUnsafe();

            InitHardware();
            InitCpuPerfCounter();

            InitTooltips();         // sensor health check later overrides the CPU tooltip

            CenterLabel(lblCpuPct, pbCPU);
            CenterLabel(lblGpuPct, pbGPU);

            // 200ms for smooth uptime; hardware reads every 5th tick (1000ms)
            _systemTimer = new Timer { Interval = 200 };
            _systemTimer.Tick += SystemTimer_Tick;
            _systemTimer.Start();

            // Give the network a moment after login before hitting GitHub.
            if (_autoCheckUpdates)
                System.Threading.Tasks.Task.Delay(15000).ContinueWith(_ => CheckForUpdate(false));
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Application.Exit();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_clickThrough)
            {
                _dragging   = true;
                _dragOffset = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                var loc = this.Location;
                loc.Offset(e.X - _dragOffset.X, e.Y - _dragOffset.Y);
                this.Location = loc;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                _dragging = false;
                SnapToEdge();
                SaveSettings();
            }
            base.OnMouseUp(e);
        }

        // Puts the HUD back where the user left it, unless that spot is no longer on any
        // monitor (display unplugged / resolution changed) — then the default corner stays.
        private void RestoreSavedPosition()
        {
            if (_savedLocation == null) return;
            var grip = _savedLocation.Value + new Size(20, 10);   // a bit inside the top-left corner
            foreach (var scr in Screen.AllScreens)
                if (scr.WorkingArea.Contains(grip))
                { this.Location = _savedLocation.Value; return; }
        }

        // The app runs elevated and auto-starts with highest privileges, so a user-writable exe
        // folder lets any non-admin process replace what gets run as admin. Say so once.
        private void WarnIfFolderUnsafe()
        {
            if (_folderWarned || !RuntimeDir.IsWritableByNonAdmins(AppDomain.CurrentDomain.BaseDirectory)) return;
            _folderWarned = true;
            SaveSettings();
            _balloonIsUpdate = false;
            _trayIcon.ShowBalloonTip(10000, "System Monitor location",
                "This folder can be modified without admin rights. For safety, move the app to " +
                @"C:\Program Files\System Monitor and turn ""Start with Windows"" off and on again.",
                ToolTipIcon.Warning);
        }

        private void SnapToEdge()
        {
            var wa = Screen.FromControl(this).WorkingArea;
            int x  = this.Left;
            int y  = this.Top;

            if      (Math.Abs(x - wa.Left)                  < SnapThreshold) x = wa.Left;
            else if (Math.Abs(x + this.Width  - wa.Right)   < SnapThreshold) x = wa.Right  - this.Width;

            if      (Math.Abs(y - wa.Top)                   < SnapThreshold) y = wa.Top;
            else if (Math.Abs(y + this.Height - wa.Bottom)  < SnapThreshold) y = wa.Bottom - this.Height;

            this.Location = new Point(x, y);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop producing work first, then let an in-flight sensor read finish before the
            // objects it uses (LHM Computer, perf counter) are torn down.
            _closing = true;
            _systemTimer?.Stop();
            var sw = Stopwatch.StartNew();
            while ((_hwReading || !_hardwareReady) && sw.ElapsedMilliseconds < 3000)
                System.Threading.Thread.Sleep(20);

            _toolTip?.Dispose();
            if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
            if (_trayIconHandle != IntPtr.Zero) DestroyIcon(_trayIconHandle);
            if (!_hwReading)
            {
                _cpuPerfCounter?.Dispose();
                if (_hardwareReady) _computer?.Close();
            }
            IntelPowerApi.Cleanup();   // only removes EnergyDriver if this app installed it
            base.OnFormClosing(e);
        }

        // BeginInvoke that is a no-op once the form is closing / disposed, so background
        // sensor and update tasks can't throw after the window is gone.
        private void PostToUi(Action action)
        {
            if (_closing || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }   // includes ObjectDisposedException
        }

        // ── CPU Sensor Access Check ───────────────────────────────────────
        // Only runs when CPU temp AND power could not be read for SensorFailThreshold
        // consecutive samples (see UpdateSensorHealth) or when the user asks from the tray.
        // Working sensors are never second-guessed, even if VBS is running.
        // Flow: not admin → explain and stop
        //       admin     → check VBS/HVCI → offer to disable if blocked
        private void CheckCpuSensorAccess(bool userInitiated = false)
        {
            if (_cpuSensorsOk)
            {
                if (userInitiated)
                    MessageBox.Show("CPU temperature / power sensors are working — nothing to change.",
                        "CPU Sensor Access", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            VbsManager.Detect();

            // Without admin rights the driver can't be used at all, so VBS is not the culprit
            // (or at least not the first thing to fix) — don't offer to weaken security for it.
            if (!IsAdministrator())
            {
                _toolTip?.SetToolTip(lblCpuTemp, "CPU sensors unavailable — run as Administrator.");
                if (userInitiated)
                    MessageBox.Show("CPU sensors can't be read because the app is not running as Administrator.",
                        "CPU Sensor Access", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Elevated — check VBS/HVCI. Secure Boot alone doesn't block the signed PawnIO driver,
            // so it is only mentioned as context, never as something to turn off.
            if (!VbsManager.IsSensorBlocked)
            {
                _toolTip?.SetToolTip(lblCpuTemp, "CPU sensors unavailable — the sensor driver could not be used.");
                if (userInitiated)
                    MessageBox.Show("CPU sensors can't be read and VBS / HVCI are not running.\n" +
                        "The sensor driver may have failed to load (see Report Issue... for details)." +
                        (VbsManager.IsSecureBootOn ? "\n\nSecure Boot is on; that normally does not block this driver." : ""),
                        "CPU Sensor Access", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // VBS/HVCI is blocking sensor access: always show "--" with an explanation,
            // whether or not the user agrees to change anything.
            _hvciEnabled = true;
            AdjustLayoutForHvci();
            _toolTip?.SetToolTip(lblCpuTemp,
                "CPU sensors unavailable — VBS / Memory Integrity is running.\n" +
                "Tray menu → \"Check CPU Sensor Access...\" for options.");

            if (_suppressVbsPrompt && !userInitiated) return;

            // Already disabled once but VBS is back → registry/bcdedit isn't enough on this PC.
            if (_vbsDisableAttempted)
            {
                if (MessageBox.Show(
                        "VBS is still running after it was disabled.\n\n" +
                        "It is probably held by Windows Security or a policy. Turn off " +
                        "\"Memory integrity\" in Windows Security → Device security → Core isolation, " +
                        "then restart.\n\nOpen Core isolation settings now?",
                        "CPU Sensor Still Blocked", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                    == DialogResult.Yes)
                {
                    try { Process.Start(new ProcessStartInfo("windowsdefender://coreisolation") { UseShellExecute = true }); }
                    catch { }
                }
                else { _suppressVbsPrompt = true; SaveSettings(); }
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("The following Windows security features are blocking CPU temperature / power sensors:\n");
            if (VbsManager.IsVbsOn)  sb.AppendLine("  • VBS (Virtualization-Based Security): ON");
            if (VbsManager.IsHvciOn) sb.AppendLine("  • HVCI (Memory Integrity): ON");
            sb.AppendLine("\nYes — disable VBS / HVCI / Hyper-V (restart required).");
            sb.AppendLine("       This lowers Windows' protection and also stops WSL2, Docker Desktop,");
            sb.AppendLine("       Windows Sandbox and Hyper-V based emulators.");
            sb.AppendLine("No  — keep them on; CPU temp / power will show \"--\". Won't ask again.");
            sb.AppendLine("       (Tray menu → \"Check CPU Sensor Access...\" to revisit.)");

            if (MessageBox.Show(sb.ToString(), "CPU Sensor Blocked",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                _suppressVbsPrompt = true;
                SaveSettings();
                return;
            }

            _suppressVbsPrompt = false;
            if (VbsManager.DisableAll(out string err))
            {
                _vbsDisableAttempted = true;
                SaveSettings();
                MessageBox.Show(
                    "VBS / HVCI / Hyper-V have been disabled.\n\nPlease restart your computer and relaunch the app.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Could not fully disable:\n{err}\n\nTry running the app as Administrator.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Auto Update ───────────────────────────────────────────────────
        // Startup check is silent (tray balloon only). Downloading and restarting always
        // needs the user's OK, and the exe is verified against the release's SHA-256.
        private void CheckForUpdate(bool userInitiated)
        {
            if (_updateBusy) return;
            _updateBusy = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                ReleaseInfo latest = null;
                string error = null;
                try { latest = Updater.CheckLatest(); }
                catch (Exception ex) { error = ex.Message; }

                try
                {
                    PostToUi(new Action(() =>
                    {
                        _updateBusy = false;
                        OnUpdateChecked(latest, error, userInitiated);
                    }));
                }
                catch { _updateBusy = false; }   // form already closed
            });
        }

        private void OnUpdateChecked(ReleaseInfo latest, string error, bool userInitiated)
        {
            if (latest == null || !Updater.IsNewer(latest))
            {
                if (userInitiated)
                    MessageBox.Show(error != null
                            ? "Could not check for updates:\n" + error
                            : $"You're up to date (v{Updater.CurrentVersion.ToString(3)}).",
                        "Check for Updates", MessageBoxButtons.OK,
                        error != null ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                return;
            }

            _pendingUpdate = latest;
            _updateItem.Text = $"Update available: v{latest.Version.ToString(3)}";

            if (userInitiated) { PromptUpdate(); return; }

            _balloonIsUpdate = true;
            _trayIcon.ShowBalloonTip(8000, "System Monitor update",
                $"Version {latest.Version.ToString(3)} is available. Click to install.", ToolTipIcon.Info);
        }

        private void PromptUpdate()
        {
            var up = _pendingUpdate;
            if (up == null || _updateBusy) return;

            if (MessageBox.Show(
                    $"Version {up.Version.ToString(3)} is available (you have {Updater.CurrentVersion.ToString(3)}).\n\n" +
                    "Download and install now? The app will restart.",
                    "System Monitor Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _updateBusy = true;
            _updateItem.Text = "Downloading update...";
            System.Threading.Tasks.Task.Run(() =>
            {
                string file = null, error = null;
                try { file = Updater.Download(up); }
                catch (Exception ex) { error = ex.Message; }

                PostToUi(new Action(() =>
                {
                    _updateBusy = false;
                    if (file == null)
                    {
                        _updateItem.Text = $"Update available: v{up.Version.ToString(3)}";
                        MessageBox.Show("Update failed:\n" + error, "System Monitor Update",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    try
                    {
                        Updater.ApplyAndRestart(file);
                        Application.Exit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not start the updater:\n" + ex.Message, "System Monitor Update",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
        }

        // ── Issue Report ──────────────────────────────────────────────────
        private const string ReportUrl = "https://www.facebook.com/PycCodeTools";

        // Nothing is sent automatically: the text goes to the clipboard and the user
        // pastes it into a message themselves. Machine name / user name are left out.
        private void ReportIssue()
        {
            string text = BuildDiagnostics();
            if (MessageBox.Show(
                    "Diagnostic info will be copied to the clipboard and the support page will open.\n" +
                    "Paste it into a message there (nothing is sent automatically).\n\n" +
                    "--------------------\n" + text,
                    "Report Issue", MessageBoxButtons.OKCancel, MessageBoxIcon.Information)
                != DialogResult.OK)
                return;

            try { Clipboard.SetText(text); } catch { }
            try { Process.Start(new ProcessStartInfo(ReportUrl) { UseShellExecute = true }); } catch { }
        }

        private string BuildDiagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("System Monitor " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            sb.AppendLine("OS: " + Environment.OSVersion + (Environment.Is64BitOperatingSystem ? " x64" : " x86"));
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                foreach (ManagementObject o in q.Get()) { sb.AppendLine("CPU: " + o["Name"]); break; }
            }
            catch { }

            VbsManager.Detect();
            sb.AppendLine("Admin: " + IsAdministrator());
            sb.AppendLine("SecureBoot: " + VbsManager.IsSecureBootOn + "  VBS: " + VbsManager.IsVbsOn + "  HVCI: " + VbsManager.IsHvciOn);
            sb.AppendLine("HardwareReady: " + _hardwareReady + "  CpuSensorsOk: " + _cpuSensorsOk + "  IntelPowerApi: " + _intelPowerOk);
            foreach (var name in new[] { "PawnIO", "EnergyDriver" })
            {
                string st;
                try { using (var sc = new ServiceController(name)) st = sc.Status.ToString(); }
                catch { st = "not installed"; }
                sb.AppendLine(name + ": " + st);
            }

            try
            {
                var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hw_error.log");
                if (File.Exists(log))
                {
                    string err = File.ReadAllText(log);
                    sb.AppendLine("hw_error.log:");
                    sb.AppendLine(err.Length > 1500 ? err.Substring(0, 1500) + "..." : err);
                }
            }
            catch { }
            return sb.ToString();
        }

        private static bool IsAdministrator()
        {
            try
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    return new System.Security.Principal.WindowsPrincipal(id)
                        .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // Called once per hardware read. Triggers CheckCpuSensorAccess only after the
        // sensors have been dead for a while, so a slow driver start doesn't cause a false alarm.
        private void UpdateSensorHealth(bool ok)
        {
            _cpuSensorsOk = ok;
            if (ok)
            {
                _sensorFailStreak = 0;
                if (_vbsDisableAttempted) { _vbsDisableAttempted = false; SaveSettings(); }
                return;
            }
            if (_sensorCheckDone || ++_sensorFailStreak < SensorFailThreshold) return;
            _sensorCheckDone = true;
            CheckCpuSensorAccess();
        }

        // When VBS/HVCI is on: hide lblPower, move lblClock up, add GPU detail row
        private void AdjustLayoutForHvci()
        {
            if (_lblGpuDetail != null) return;   // already applied (re-check from tray menu)
            lblPower.Visible = false;
            lblClock.Top     = lblPower.Top;

            _lblGpuDetail = new Guna.UI2.WinForms.Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font      = lblClock.Font,
                ForeColor = lblClock.ForeColor,
                Location  = new Point(lblClock.Left, lblPower.Top + 18),
                Size      = new Size(170, 15),
                Text      = "HOT  --°C  |  GPU  --W",
            };
            this.Controls.Add(_lblGpuDetail);

            _lblSsdTemp = new Guna.UI2.WinForms.Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font      = lblClock.Font,
                ForeColor = Color.FromArgb(120, 220, 255),
                Location  = new Point(lblClock.Left, lblPower.Top + 36),
                Size      = new Size(170, 15),
                Text      = "SSD  --°C",
            };
            this.Controls.Add(_lblSsdTemp);
        }

        // ── Acrylic Blur ──────────────────────────────────────────────────
        private void EnableAcrylic()
        {
            // Dark navy-purple tint with semi-transparency; ABGR packed int:
            // A=160 (62% opaque tint), B=28, G=0, R=12 → cyberpunk blue-purple
            int gradientColor = (160 << 24) | (28 << 16) | (0 << 8) | 12; // 0xA01C000C

            var accent = new ACCENT_POLICY
            {
                AccentState   = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                AccentFlags   = 2,
                GradientColor = gradientColor,
            };

            int    sz  = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute  = 19, // WCA_ACCENT_POLICY
                    Data       = ptr,
                    SizeOfData = sz,
                };
                SetWindowCompositionAttribute(this.Handle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        // ── Click-Through ─────────────────────────────────────────────────
        private void SetClickThrough(bool enable)
        {
            _clickThrough = enable;
            int style = GetWindowLong(this.Handle, GWL_EXSTYLE);
            SetWindowLong(this.Handle, GWL_EXSTYLE,
                enable ? style | WS_EX_TRANSPARENT
                       : style & ~WS_EX_TRANSPARENT);
        }

        // ── Auto-Start (Scheduled Task — bypasses UAC on startup) ────────
        private const string TaskName = "FrmSysMonitor";

        private static bool IsAutoStartEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks",
                    $"/query /tn \"{TaskName}\" /fo LIST")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
                using (var p = Process.Start(psi))
                { p.WaitForExit(); return p.ExitCode == 0; }
            }
            catch { return false; }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                string args = enable
                    ? string.Format(
                        "/create /tn \"{0}\" /tr \"\\\"{1}\\\"\" /sc onlogon /rl highest /f",
                        TaskName, Application.ExecutablePath)
                    : $"/delete /tn \"{TaskName}\" /f";

                var psi = new ProcessStartInfo("schtasks", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
                using (var p = Process.Start(psi)) p.WaitForExit();
            }
            catch { }
        }

        // ── Accent Color ──────────────────────────────────────────────────
        private void ApplyAccentColor(Color c)
        {
            _accentColor = c;

            // Darker gradient stop: desaturate toward navy
            Color dark = Color.FromArgb(
                Math.Max(0, c.R - 190),
                Math.Max(0, c.G - 190),
                Math.Min(255, c.B + 20));

            // Dimmed label variant (readable but softer than the ring)
            Color dim = Color.FromArgb(
                Math.Min(255, (int)(c.R * 0.55f + 68)),
                Math.Min(255, (int)(c.G * 0.55f + 68)),
                Math.Min(255, (int)(c.B * 0.55f + 68)));

            pbCPU.ProgressColor  = c;   pbCPU.ProgressColor2  = dark;
            pbGPU.ProgressColor  = c;   pbGPU.ProgressColor2  = dark;
            pbVram.ProgressColor = c;

            lblCpuPct.ForeColor = c;
            lblGpuPct.ForeColor = c;
            lblCpuTag.ForeColor = dim;
            lblGpuTag.ForeColor = dim;
            lblCpuTemp.ForeColor = dim;
            lblRam.ForeColor     = dim;
            lblUpTime.ForeColor  = dim;
            lblPower.ForeColor   = dim;
            lblVramTag.ForeColor = dim;
            // lblGpuTemp and lblClock use dynamic colors — not overridden here

            guna2Separator1.FillColor = Color.FromArgb(55, c);

            // Refresh tray icon if already created
            if (_trayIcon != null)
                _trayIcon.Icon = CreateTrayIcon();
        }

        private void LoadSettings()
        {
            if (!File.Exists(SettingsPath)) { ApplyAccentColor(_accentColor); return; }
            try
            {
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "AccentColor")
                        try { _accentColor = ColorTranslator.FromHtml("#" + val); } catch { }
                    else if (key == "AutoCheckUpdates")     _autoCheckUpdates    = val != "0";
                    else if (key == "SuppressVbsPrompt")    _suppressVbsPrompt   = val == "1";
                    else if (key == "VbsDisableAttempted")  _vbsDisableAttempted = val == "1";
                    else if (key == "AutoStartConfigured")  _autoStartConfigured = val == "1";
                    else if (key == "FolderWarned")         _folderWarned        = val == "1";
                    else if (key == "Location")
                    {
                        var xy = val.Split(',');
                        if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
                            _savedLocation = new Point(x, y);
                    }
                }
            }
            catch { }
            ApplyAccentColor(_accentColor);
        }

        private void SaveSettings()
        {
            try { File.WriteAllText(SettingsPath,
                // Hex only: ToHtml() returns names like "Cyan"/"Red", which LoadSettings can't parse back.
                "AccentColor=" + $"{_accentColor.R:X2}{_accentColor.G:X2}{_accentColor.B:X2}" + "\r\n" +
                "AutoCheckUpdates=" + (_autoCheckUpdates ? "1" : "0") + "\r\n" +
                "SuppressVbsPrompt=" + (_suppressVbsPrompt ? "1" : "0") + "\r\n" +
                "VbsDisableAttempted=" + (_vbsDisableAttempted ? "1" : "0") + "\r\n" +
                "AutoStartConfigured=" + (_autoStartConfigured ? "1" : "0") + "\r\n" +
                "FolderWarned=" + (_folderWarned ? "1" : "0") + "\r\n" +
                "Location=" + this.Left + "," + this.Top); }
            catch { }
        }

        // ── Tooltips ──────────────────────────────────────────────────────
        private void InitTooltips()
        {
            _toolTip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 500, ReshowDelay = 300, ShowAlways = true };
            _toolTip.SetToolTip(pbCPU,         "CPU Load — Current processor utilization (%)");
            _toolTip.SetToolTip(lblCpuTag,      "CPU Load — Current processor utilization (%)");
            _toolTip.SetToolTip(pbGPU,          "GPU Load — Current graphics card utilization (%)");
            _toolTip.SetToolTip(lblGpuTag,      "GPU Load — Current graphics card utilization (%)");
            _toolTip.SetToolTip(lblCpuTemp,     "CPU Temperature — Processor die temperature (°C)");
            _toolTip.SetToolTip(lblGpuTemp,     "GPU Temperature — Graphics card temperature (°C)");
            _toolTip.SetToolTip(lblRam,         "RAM Usage — Memory in use / Total installed (GB)");
            _toolTip.SetToolTip(lblUpTime,      "System Uptime — Time elapsed since last boot");
            _toolTip.SetToolTip(lblPower,       "Power Draw — CPU | GPU power consumption (Watts)");
            _toolTip.SetToolTip(lblClock,       "CPU Clock — Highest active core frequency (GHz)");
            _toolTip.SetToolTip(lblVramTag,     "VRAM Usage — GPU memory in use / Total VRAM (GB)");
            _toolTip.SetToolTip(pbVram,         "VRAM Usage — GPU memory in use / Total VRAM (GB)");
            _toolTip.SetToolTip(lblMachineName, "Computer Name — This machine's network hostname");
        }

        // ── System Tray ───────────────────────────────────────────────────
        private void InitTray()
        {
            // Auto-start: register once on the very first run; afterwards the user's
            // choice (tray → "Start with Windows") is respected and never re-enabled.
            _autoStart = IsAutoStartEnabled();
            if (!_autoStart && !_autoStartConfigured) { SetAutoStart(true); _autoStart = IsAutoStartEnabled(); }
            if (!_autoStartConfigured) { _autoStartConfigured = true; SaveSettings(); }

            SetClickThrough(true);
            var clickThroughItem = new ToolStripMenuItem("Click-Through: ON");
            clickThroughItem.Click += (s, ev) =>
            {
                bool next = !_clickThrough;
                SetClickThrough(next);
                clickThroughItem.Text = next ? "Click-Through: ON" : "Click-Through: OFF";
            };

            var showHideItem = new ToolStripMenuItem("Hide HUD");
            showHideItem.Click += (s, ev) =>
            {
                this.Visible = !this.Visible;
                showHideItem.Text = this.Visible ? "Hide HUD" : "Show HUD";
            };

            var autoStartItem = new ToolStripMenuItem("Start with Windows")
            {
                CheckOnClick = true,
                Checked      = _autoStart,
            };
            autoStartItem.Click += (s, ev) =>
            {
                SetAutoStart(autoStartItem.Checked);
                _autoStart = IsAutoStartEnabled();
                autoStartItem.Checked = _autoStart;
                _autoStartConfigured = true;
                SaveSettings();
            };

            var colorItem = new ToolStripMenuItem("Accent Color...");
            colorItem.Click += (s, ev) =>
            {
                using (var dlg = new ColorDialog { Color = _accentColor, FullOpen = true })
                    if (dlg.ShowDialog() == DialogResult.OK)
                    { ApplyAccentColor(dlg.Color); SaveSettings(); }
            };

            var sensorItem = new ToolStripMenuItem("Check CPU Sensor Access...");
            sensorItem.Click += (s, ev) => CheckCpuSensorAccess(userInitiated: true);

            _updateItem = new ToolStripMenuItem("Check for Updates...");
            _updateItem.Click += (s, ev) =>
            {
                if (_pendingUpdate != null) PromptUpdate();
                else CheckForUpdate(true);
            };

            var autoUpdateItem = new ToolStripMenuItem("Check for updates on startup")
            {
                CheckOnClick = true,
                Checked      = _autoCheckUpdates,
            };
            autoUpdateItem.Click += (s, ev) => { _autoCheckUpdates = autoUpdateItem.Checked; SaveSettings(); };

            var reportItem = new ToolStripMenuItem("Report Issue...");
            reportItem.Click += (s, ev) => ReportIssue();

            var monitorsMenu = new ToolStripMenuItem("Move to Monitor");

            var menu = new ContextMenuStrip();
            menu.Opening += (s, ev) =>
            {
                monitorsMenu.DropDownItems.Clear();
                var screens = Screen.AllScreens;
                monitorsMenu.Enabled = screens.Length > 1;
                for (int i = 0; i < screens.Length; i++)
                {
                    var scr = screens[i];
                    var idx = i;
                    var mi = new ToolStripMenuItem(
                        $"Monitor {i + 1}{(scr.Primary ? " ★" : "")}  {scr.Bounds.Width}×{scr.Bounds.Height}");
                    mi.Click += (ss, eev) =>
                    {
                        var wa = Screen.AllScreens[idx].WorkingArea;
                        this.Location = new Point(wa.Right - this.Width - 10, wa.Top + 10);
                        SaveSettings();
                    };
                    monitorsMenu.DropDownItems.Add(mi);
                }
            };

            menu.Items.Add("System Monitor").Enabled = false;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(clickThroughItem);
            menu.Items.Add(showHideItem);
            menu.Items.Add(autoStartItem);
            menu.Items.Add(colorItem);
            menu.Items.Add(sensorItem);
            menu.Items.Add(reportItem);
            menu.Items.Add(_updateItem);
            menu.Items.Add(autoUpdateItem);
            menu.Items.Add(monitorsMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, ev) => Application.Exit());

            _trayIcon = new NotifyIcon
            {
                Icon             = CreateTrayIcon(),
                Text             = "System Monitor",
                Visible          = true,
                ContextMenuStrip = menu,
            };

            _trayIcon.BalloonTipClicked += (s, ev) =>
            {
                if (_balloonIsUpdate && _pendingUpdate != null) PromptUpdate();
            };

            _trayIcon.MouseClick += (s, ev) =>
            {
                if (ev.Button != MouseButtons.Left) return;
                this.Visible = !this.Visible;
                showHideItem.Text = this.Visible ? "Hide HUD" : "Show HUD";
            };
        }

        private Icon CreateTrayIcon()
        {
            if (_trayIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(_accentColor))             g.FillEllipse(b, 1, 1, 13, 13);
                using (var p = new Pen(Color.FromArgb(0, 0, 180), 1.5f)) g.DrawEllipse(p, 1, 1, 12, 12);
                _trayIconHandle = bmp.GetHicon();
                return Icon.FromHandle(_trayIconHandle);
            }
        }

        // ── Hardware ──────────────────────────────────────────────────────
        private void InitHardware()
        {
            _computer = new Computer
            {
                IsCpuEnabled         = true,
                IsGpuEnabled         = true,
                IsMemoryEnabled      = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled     = true,
            };

            // Driver install + DLL init are slow kernel ops — run off the UI thread.
            // _computer.Open() must follow PawnIo so LHM finds the MSR service.
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    PawnIoInstaller.EnsureInstalled();
                    _computer.Open();
                    _intelPowerOk = IntelPowerApi.TryInitialize();
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hw_error.log"),
                        ex.ToString()); }
                    catch { }
                }
                finally { _hardwareReady = true; }
            });
        }

        private void InitCpuPerfCounter()
        {
            try
            {
                _cpuPerfCounter = new PerformanceCounter(
                    "Processor Information", "% Processor Performance", "_Total", true);
                using (var q = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
                foreach (ManagementObject o in q.Get())
                {
                    _cpuMaxClockMHz = Convert.ToSingle(o["MaxClockSpeed"]);
                    break;
                }
            }
            catch { }
        }

        // ── Timer — 200 ms UI tick / 1000 ms hw tick ──────────────────────
        private volatile bool _hwReading;
        private volatile bool _closing;

        private void SystemTimer_Tick(object sender, EventArgs e)
        {
            // Uptime is always updated on UI thread — lightweight kernel call, no lag.
            var up = TimeSpan.FromMilliseconds(GetTickCount64());
            lblUpTime.Text = $"UP  {(int)up.TotalHours:D2}:{up.Minutes:D2}:{up.Seconds:D2}";

            if (++_hwTick < 5) return;
            _hwTick = 0;

            if (!_hardwareReady || _hwReading) return;
            _hwReading = true;

            // All sensor I/O runs on a thread-pool thread; only label updates touch the UI.
            System.Threading.Tasks.Task.Run(() =>
            {
                try { ReadAllSensors(); }
                finally { _hwReading = false; }
            });
        }

        private void ReadAllSensors()
        {
            var snap = SensorReader.Read(_computer);

            // PerfCounter (APERF/MPERF-based effective clock) is primary;
            // LHM average is fallback for machines where PerfCounter fails.
            float cpuMHz = GetCpuClockPerf();
            if (cpuMHz <= 0) cpuMHz = snap.CpuClockAvgMHz;

            // Intel Energy API override (only if returns real data)
            if (_intelPowerOk
                && IntelPowerApi.GetPowerData(0, 0, out double pkgW, out _, out _)
                && pkgW > 0)
                snap.CpuWatts = (float)pkgW;

            // Marshal all UI updates back to the UI thread at once
            PostToUi(() => ApplySensorResults(snap, cpuMHz));
        }

        private void ApplySensorResults(SensorSnapshot s, float cpuMHz)
        {
            if (s.CpuLoad.HasValue)
            {
                _lastCpuLoad   = s.CpuLoad.Value;
                pbCPU.Value    = _lastCpuLoad;
                lblCpuPct.Text = $"{_lastCpuLoad}%";
                CenterLabel(lblCpuPct, pbCPU);
            }
            if (s.GpuLoad.HasValue)
            {
                _lastGpuLoad   = s.GpuLoad.Value;
                pbGPU.Value    = _lastGpuLoad;
                lblGpuPct.Text = $"{_lastGpuLoad}%";
                CenterLabel(lblGpuPct, pbGPU);
            }
            if (s.GpuTemp > 0)
            {
                lblGpuTemp.Text      = $"GPU  {(int)s.GpuTemp}°C";
                lblGpuTemp.ForeColor = s.GpuTemp >= 80f ? Color.Red
                                     : s.GpuTemp >= 70f ? Color.Orange
                                                        : Color.Cyan;
            }

            lblCpuTemp.Text = s.CpuTemp.HasValue ? $"CPU  {s.CpuTemp}°C" : "CPU  --°C";
            UpdateSensorHealth(s.CpuTemp.HasValue || s.CpuWatts > 0);

            if (s.RamUsedGB > 0 || s.RamAvailableGB > 0)
                lblRam.Text = $"RAM  {s.RamUsedGB:F1} / {s.RamUsedGB + s.RamAvailableGB:F0} GB";

            if (cpuMHz > 0)
            {
                float ghz = cpuMHz / 1000f;
                lblClock.Text      = $"CLK  {ghz:F2} GHz";
                lblClock.ForeColor = ghz >= 5.0f ? Color.Orange : Color.FromArgb(120, 220, 255);
            }
            else
            {
                lblClock.Text      = "CLK  -- GHz";
                lblClock.ForeColor = Color.FromArgb(120, 220, 255);
            }

            if (_hvciEnabled)
            {
                if (_lblGpuDetail != null)
                    _lblGpuDetail.Text =
                        $"HOT  {(s.GpuHotspotTemp > 0 ? $"{(int)s.GpuHotspotTemp}°C" : "--°C")}" +
                        $"  |  GPU  {(s.GpuWatts > 0 ? $"{(int)s.GpuWatts}W" : "--W")}";
                if (_lblSsdTemp != null)
                    _lblSsdTemp.Text = s.SsdTemp > 0 ? $"SSD  {(int)s.SsdTemp}°C" : "SSD  --°C";
            }
            else
            {
                lblPower.Text = $"PWR  {Watts(s.CpuWatts)} | {Watts(s.GpuWatts)}";
            }

            if (s.VramTotalMB > 0)
            {
                lblVramTag.Text = $"VRAM {s.VramUsedMB / 1024f:F1}/{s.VramTotalMB / 1024f:F0} GB";
                pbVram.Value    = Math.Max(0, Math.Min((int)(s.VramUsedMB / s.VramTotalMB * 100), 100));
            }

            UpdateTrayTooltip(s);
        }

        private static string Watts(float w) => w > 0 ? $"{w:F0}W" : "--W";

        private void UpdateTrayTooltip(SensorSnapshot s)
        {
            if (_trayIcon == null) return;

            string cpuTemp = s.CpuTemp.HasValue ? s.CpuTemp + "°C" : "--°C";
            string ram     = s.RamUsedGB > 0 ? $"{s.RamUsedGB:F1} / {s.RamUsedGB + s.RamAvailableGB:F0} GB" : "-- GB";

            // NotifyIcon.Text is capped at 127 chars by Windows
            string tip = $"CPU  {_lastCpuLoad}%  {cpuTemp}  {Watts(s.CpuWatts)}\nGPU  {_lastGpuLoad}%  {Watts(s.GpuWatts)}\nRAM  {ram}";
            if (tip.Length > 127) tip = tip.Substring(0, 127);
            _trayIcon.Text = tip;
        }

        private float GetCpuClockPerf()
        {
            try
            {
                if (_cpuPerfCounter == null || _cpuMaxClockMHz <= 0) return 0;
                return _cpuPerfCounter.NextValue() / 100f * _cpuMaxClockMHz;
            }
            catch { return 0; }
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static void CenterLabel(
            Guna.UI2.WinForms.Guna2HtmlLabel label,
            System.Windows.Forms.Control parent)
        {
            label.Width = parent.Width;
            label.Left  = 0;
            var sz     = TextRenderer.MeasureText(label.Text, label.Font);
            // Guna2HtmlLabel adds ~2px internal top-padding; subtract to compensate
            label.Top  = (parent.Height - sz.Height) / 2 - 2;
        }
    }
}
