namespace frm_sys_monitor
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.guna2BorderlessForm1 = new Guna.UI2.WinForms.Guna2BorderlessForm(this.components);
            this.guna2ShadowForm1 = new Guna.UI2.WinForms.Guna2ShadowForm(this.components);
            this.pbCPU = new Guna.UI2.WinForms.Guna2CircleProgressBar();
            this.lblCpuPct = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.pbGPU = new Guna.UI2.WinForms.Guna2CircleProgressBar();
            this.lblGpuPct = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblCpuTag = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblGpuTag = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblCpuTemp = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblGpuTemp = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblRam = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblUpTime = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblPower = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblClock = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.lblVramTag = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.pbVram = new Guna.UI2.WinForms.Guna2ProgressBar();
            this.guna2Separator1 = new Guna.UI2.WinForms.Guna2Separator();
            this.lblMachineName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.pbCPU.SuspendLayout();
            this.pbGPU.SuspendLayout();
            this.SuspendLayout();
            // 
            // guna2BorderlessForm1
            // 
            this.guna2BorderlessForm1.BorderRadius = 20;
            this.guna2BorderlessForm1.ContainerControl = this;
            this.guna2BorderlessForm1.DockIndicatorTransparencyValue = 0.6D;
            // 
            // pbCPU
            // 
            this.pbCPU.Animated = true;
            this.pbCPU.BackColor = System.Drawing.Color.Transparent;
            this.pbCPU.Controls.Add(this.lblCpuPct);
            this.pbCPU.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(0)))), ((int)(((byte)(200)))), ((int)(((byte)(255)))));
            this.pbCPU.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Bold);
            this.pbCPU.ForeColor = System.Drawing.Color.Cyan;
            this.pbCPU.Location = new System.Drawing.Point(15, 12);
            this.pbCPU.Minimum = 0;
            this.pbCPU.Name = "pbCPU";
            this.pbCPU.ProgressColor = System.Drawing.Color.Cyan;
            this.pbCPU.ProgressColor2 = System.Drawing.Color.Blue;
            this.pbCPU.ProgressThickness = 8;
            this.pbCPU.ShadowDecoration.Mode = Guna.UI2.WinForms.Enums.ShadowMode.Circle;
            this.pbCPU.Size = new System.Drawing.Size(100, 100);
            this.pbCPU.TabIndex = 0;
            // 
            // lblCpuPct
            // 
            this.lblCpuPct.AutoSize = false;
            this.lblCpuPct.BackColor = System.Drawing.Color.Transparent;
            this.lblCpuPct.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Bold);
            this.lblCpuPct.ForeColor = System.Drawing.Color.Cyan;
            this.lblCpuPct.Location = new System.Drawing.Point(0, 38);
            this.lblCpuPct.Name = "lblCpuPct";
            this.lblCpuPct.Size = new System.Drawing.Size(100, 24);
            this.lblCpuPct.TabIndex = 0;
            this.lblCpuPct.Text = "--%";
            // 
            // pbGPU
            // 
            this.pbGPU.Animated = true;
            this.pbGPU.BackColor = System.Drawing.Color.Transparent;
            this.pbGPU.Controls.Add(this.lblGpuPct);
            this.pbGPU.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(0)))), ((int)(((byte)(200)))), ((int)(((byte)(255)))));
            this.pbGPU.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Bold);
            this.pbGPU.ForeColor = System.Drawing.Color.Cyan;
            this.pbGPU.Location = new System.Drawing.Point(305, 12);
            this.pbGPU.Minimum = 0;
            this.pbGPU.Name = "pbGPU";
            this.pbGPU.ProgressColor = System.Drawing.Color.Cyan;
            this.pbGPU.ProgressColor2 = System.Drawing.Color.Blue;
            this.pbGPU.ProgressThickness = 8;
            this.pbGPU.ShadowDecoration.Mode = Guna.UI2.WinForms.Enums.ShadowMode.Circle;
            this.pbGPU.Size = new System.Drawing.Size(100, 100);
            this.pbGPU.TabIndex = 2;
            // 
            // lblGpuPct
            // 
            this.lblGpuPct.AutoSize = false;
            this.lblGpuPct.BackColor = System.Drawing.Color.Transparent;
            this.lblGpuPct.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Bold);
            this.lblGpuPct.ForeColor = System.Drawing.Color.Cyan;
            this.lblGpuPct.Location = new System.Drawing.Point(0, 38);
            this.lblGpuPct.Name = "lblGpuPct";
            this.lblGpuPct.Size = new System.Drawing.Size(100, 24);
            this.lblGpuPct.TabIndex = 0;
            this.lblGpuPct.Text = "--%";
            // 
            // lblCpuTag
            // 
            this.lblCpuTag.BackColor = System.Drawing.Color.Transparent;
            this.lblCpuTag.Font = new System.Drawing.Font("Consolas", 7.5F);
            this.lblCpuTag.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.lblCpuTag.Location = new System.Drawing.Point(15, 116);
            this.lblCpuTag.Name = "lblCpuTag";
            this.lblCpuTag.Size = new System.Drawing.Size(63, 14);
            this.lblCpuTag.TabIndex = 4;
            this.lblCpuTag.Text = "◈  CPU LOAD  ◈";
            // 
            // lblGpuTag
            // 
            this.lblGpuTag.BackColor = System.Drawing.Color.Transparent;
            this.lblGpuTag.Font = new System.Drawing.Font("Consolas", 7.5F);
            this.lblGpuTag.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.lblGpuTag.Location = new System.Drawing.Point(305, 116);
            this.lblGpuTag.Name = "lblGpuTag";
            this.lblGpuTag.Size = new System.Drawing.Size(63, 14);
            this.lblGpuTag.TabIndex = 5;
            this.lblGpuTag.Text = "◈  GPU LOAD  ◈";
            // 
            // lblCpuTemp
            // 
            this.lblCpuTemp.BackColor = System.Drawing.Color.Transparent;
            this.lblCpuTemp.Font = new System.Drawing.Font("Consolas", 9F);
            this.lblCpuTemp.ForeColor = System.Drawing.Color.Cyan;
            this.lblCpuTemp.Location = new System.Drawing.Point(128, 15);
            this.lblCpuTemp.Name = "lblCpuTemp";
            this.lblCpuTemp.Size = new System.Drawing.Size(59, 16);
            this.lblCpuTemp.TabIndex = 6;
            this.lblCpuTemp.Text = "CPU  --°C";
            // 
            // lblGpuTemp
            // 
            this.lblGpuTemp.BackColor = System.Drawing.Color.Transparent;
            this.lblGpuTemp.Font = new System.Drawing.Font("Consolas", 9F);
            this.lblGpuTemp.ForeColor = System.Drawing.Color.Cyan;
            this.lblGpuTemp.Location = new System.Drawing.Point(128, 37);
            this.lblGpuTemp.Name = "lblGpuTemp";
            this.lblGpuTemp.Size = new System.Drawing.Size(59, 16);
            this.lblGpuTemp.TabIndex = 7;
            this.lblGpuTemp.Text = "GPU  --°C";
            // 
            // lblRam
            // 
            this.lblRam.BackColor = System.Drawing.Color.Transparent;
            this.lblRam.Font = new System.Drawing.Font("Consolas", 9F);
            this.lblRam.ForeColor = System.Drawing.Color.Cyan;
            this.lblRam.Location = new System.Drawing.Point(128, 59);
            this.lblRam.Name = "lblRam";
            this.lblRam.Size = new System.Drawing.Size(101, 16);
            this.lblRam.TabIndex = 8;
            this.lblRam.Text = "RAM  -- / -- GB";
            // 
            // lblUpTime
            // 
            this.lblUpTime.BackColor = System.Drawing.Color.Transparent;
            this.lblUpTime.Font = new System.Drawing.Font("Consolas", 9F);
            this.lblUpTime.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(210)))), ((int)(((byte)(255)))));
            this.lblUpTime.Location = new System.Drawing.Point(128, 81);
            this.lblUpTime.Name = "lblUpTime";
            this.lblUpTime.Size = new System.Drawing.Size(80, 16);
            this.lblUpTime.TabIndex = 9;
            this.lblUpTime.Text = "UP  --:--:--";
            // 
            // lblPower
            // 
            this.lblPower.BackColor = System.Drawing.Color.Transparent;
            this.lblPower.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.lblPower.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(220)))), ((int)(((byte)(255)))));
            this.lblPower.Location = new System.Drawing.Point(128, 103);
            this.lblPower.Name = "lblPower";
            this.lblPower.Size = new System.Drawing.Size(99, 15);
            this.lblPower.TabIndex = 10;
            this.lblPower.Text = "PWR: -- W | -- W";
            // 
            // lblClock
            // 
            this.lblClock.BackColor = System.Drawing.Color.Transparent;
            this.lblClock.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.lblClock.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(220)))), ((int)(((byte)(255)))));
            this.lblClock.Location = new System.Drawing.Point(128, 121);
            this.lblClock.Name = "lblClock";
            this.lblClock.Size = new System.Drawing.Size(69, 15);
            this.lblClock.TabIndex = 11;
            this.lblClock.Text = "CLK: -- GHz";
            // 
            // lblVramTag
            // 
            this.lblVramTag.BackColor = System.Drawing.Color.Transparent;
            this.lblVramTag.Font = new System.Drawing.Font("Consolas", 7.5F);
            this.lblVramTag.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.lblVramTag.Location = new System.Drawing.Point(305, 132);
            this.lblVramTag.Name = "lblVramTag";
            this.lblVramTag.Size = new System.Drawing.Size(83, 14);
            this.lblVramTag.TabIndex = 12;
            this.lblVramTag.Text = "VRAM: -- / -- GB";
            // 
            // pbVram
            // 
            this.pbVram.BorderRadius = 3;
            this.pbVram.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(0)))), ((int)(((byte)(200)))), ((int)(((byte)(255)))));
            this.pbVram.Location = new System.Drawing.Point(305, 148);
            this.pbVram.Name = "pbVram";
            this.pbVram.ProgressColor = System.Drawing.Color.Cyan;
            this.pbVram.ProgressColor2 = System.Drawing.Color.Blue;
            this.pbVram.Size = new System.Drawing.Size(100, 7);
            this.pbVram.TabIndex = 13;
            this.pbVram.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
            // 
            // guna2Separator1
            // 
            this.guna2Separator1.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.guna2Separator1.Location = new System.Drawing.Point(10, 162);
            this.guna2Separator1.Name = "guna2Separator1";
            this.guna2Separator1.Size = new System.Drawing.Size(400, 1);
            this.guna2Separator1.TabIndex = 14;
            // 
            // lblMachineName
            // 
            this.lblMachineName.BackColor = System.Drawing.Color.Transparent;
            this.lblMachineName.Font = new System.Drawing.Font("Consolas", 7.5F);
            this.lblMachineName.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(180)))), ((int)(((byte)(255)))));
            this.lblMachineName.Location = new System.Drawing.Point(10, 170);
            this.lblMachineName.Name = "lblMachineName";
            this.lblMachineName.Size = new System.Drawing.Size(18, 14);
            this.lblMachineName.TabIndex = 15;
            this.lblMachineName.Text = "...";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(10)))), ((int)(((byte)(10)))), ((int)(((byte)(25)))));
            this.ClientSize = new System.Drawing.Size(420, 192);
            this.Controls.Add(this.lblMachineName);
            this.Controls.Add(this.guna2Separator1);
            this.Controls.Add(this.pbVram);
            this.Controls.Add(this.lblVramTag);
            this.Controls.Add(this.lblClock);
            this.Controls.Add(this.lblPower);
            this.Controls.Add(this.lblUpTime);
            this.Controls.Add(this.lblRam);
            this.Controls.Add(this.lblGpuTemp);
            this.Controls.Add(this.lblCpuTemp);
            this.Controls.Add(this.lblGpuTag);
            this.Controls.Add(this.lblCpuTag);
            this.Controls.Add(this.pbGPU);
            this.Controls.Add(this.pbCPU);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(420, 192);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(420, 192);
            this.Name = "Form1";
            this.Opacity = 0.88D;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "System Monitor";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Form1_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Form1_KeyDown);
            this.pbCPU.ResumeLayout(false);
            this.pbGPU.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Guna.UI2.WinForms.Guna2BorderlessForm     guna2BorderlessForm1;
        private Guna.UI2.WinForms.Guna2ShadowForm         guna2ShadowForm1;
        private Guna.UI2.WinForms.Guna2CircleProgressBar  pbCPU;
        private Guna.UI2.WinForms.Guna2CircleProgressBar  pbGPU;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblCpuPct;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblGpuPct;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblCpuTag;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblGpuTag;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblCpuTemp;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblGpuTemp;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblRam;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblUpTime;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblPower;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblClock;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblVramTag;
        private Guna.UI2.WinForms.Guna2ProgressBar        pbVram;
        private Guna.UI2.WinForms.Guna2Separator          guna2Separator1;
        private Guna.UI2.WinForms.Guna2HtmlLabel          lblMachineName;
    }
}
