#nullable enable

namespace WalkieTalkieApp
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer? components = null;

        // Se inicializan en InitializeComponent(), llamado desde el constructor.
        internal System.Windows.Forms.Panel topPanel = null!;
        internal System.Windows.Forms.PictureBox picUser = null!;
        internal System.Windows.Forms.Label lblUserName = null!;
        internal System.Windows.Forms.Label lblUserStatus = null!;
        internal System.Windows.Forms.Button btnSettings = null!;
        internal System.Windows.Forms.Button btnMute = null!;

        internal System.Windows.Forms.SplitContainer splitMain = null!;
        internal System.Windows.Forms.Label lblContactos = null!;
        internal System.Windows.Forms.ListBox lstContactos = null!;
        internal System.Windows.Forms.Label lblHistorial = null!;
        internal System.Windows.Forms.ListBox lstHistorial = null!;

        internal System.Windows.Forms.Panel bottomPanel = null!;
        internal WalkieTalkieApp.VuMeter vuMeter = null!;
        internal System.Windows.Forms.Label lblMicHint = null!;
        internal System.Windows.Forms.Button btnRecord = null!;
        internal System.Windows.Forms.Button btnPlay = null!;

        internal System.Windows.Forms.Panel statusPanel = null!;
        internal System.Windows.Forms.Label lblStatus = null!;
        internal System.Windows.Forms.Label lblAir = null!;

        internal System.Windows.Forms.NotifyIcon trayIcon = null!;
        internal System.Windows.Forms.ContextMenuStrip trayMenu = null!;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.topPanel = new System.Windows.Forms.Panel();
            this.picUser = new System.Windows.Forms.PictureBox();
            this.lblUserName = new System.Windows.Forms.Label();
            this.lblUserStatus = new System.Windows.Forms.Label();
            this.btnSettings = new System.Windows.Forms.Button();
            this.btnMute = new System.Windows.Forms.Button();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.lblContactos = new System.Windows.Forms.Label();
            this.lstContactos = new System.Windows.Forms.ListBox();
            this.lblHistorial = new System.Windows.Forms.Label();
            this.lstHistorial = new System.Windows.Forms.ListBox();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.vuMeter = new WalkieTalkieApp.VuMeter();
            this.lblMicHint = new System.Windows.Forms.Label();
            this.btnRecord = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.statusPanel = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblAir = new System.Windows.Forms.Label();
            this.trayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.trayIcon = new System.Windows.Forms.NotifyIcon(this.components);

            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picUser)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.statusPanel.SuspendLayout();
            this.SuspendLayout();

            //
            // topPanel
            //
            this.topPanel.BackColor = System.Drawing.Color.FromArgb(24, 25, 28);
            this.topPanel.Controls.Add(this.lblUserStatus);
            this.topPanel.Controls.Add(this.lblUserName);
            this.topPanel.Controls.Add(this.picUser);
            this.topPanel.Controls.Add(this.btnMute);
            this.topPanel.Controls.Add(this.btnSettings);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(14, 12, 14, 12);
            this.topPanel.Size = new System.Drawing.Size(800, 72);
            this.topPanel.TabIndex = 0;

            //
            // picUser
            //
            this.picUser.Location = new System.Drawing.Point(14, 12);
            this.picUser.Name = "picUser";
            this.picUser.Size = new System.Drawing.Size(48, 48);
            this.picUser.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picUser.TabStop = false;

            //
            // lblUserName
            //
            this.lblUserName.AutoSize = true;
            this.lblUserName.Location = new System.Drawing.Point(74, 14);
            this.lblUserName.Name = "lblUserName";
            this.lblUserName.Size = new System.Drawing.Size(110, 25);
            this.lblUserName.Text = "Usuario";

            //
            // lblUserStatus
            //
            this.lblUserStatus.AutoSize = true;
            this.lblUserStatus.Location = new System.Drawing.Point(76, 42);
            this.lblUserStatus.Name = "lblUserStatus";
            this.lblUserStatus.Size = new System.Drawing.Size(110, 15);
            this.lblUserStatus.Text = "Conectando...";

            //
            // btnSettings
            //
            this.btnSettings.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnSettings.Location = new System.Drawing.Point(742, 20);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(44, 32);
            this.btnSettings.TabIndex = 12;
            this.btnSettings.Text = "⚙";
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);

            //
            // btnMute
            //
            this.btnMute.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnMute.Location = new System.Drawing.Point(692, 20);
            this.btnMute.Name = "btnMute";
            this.btnMute.Size = new System.Drawing.Size(44, 32);
            this.btnMute.TabIndex = 11;
            this.btnMute.Text = "🔊";
            this.btnMute.Click += new System.EventHandler(this.btnMute_Click);

            //
            // splitMain
            //
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 72);
            this.splitMain.Name = "splitMain";
            this.splitMain.Size = new System.Drawing.Size(800, 314);
            this.splitMain.SplitterDistance = 230;
            this.splitMain.SplitterWidth = 6;
            this.splitMain.TabIndex = 1;
            this.splitMain.Panel1MinSize = 170;
            this.splitMain.Panel2MinSize = 240;
            this.splitMain.Panel1.Padding = new System.Windows.Forms.Padding(14, 10, 4, 8);
            this.splitMain.Panel2.Padding = new System.Windows.Forms.Padding(4, 10, 14, 8);
            this.splitMain.Panel1.Controls.Add(this.lstContactos);
            this.splitMain.Panel1.Controls.Add(this.lblContactos);
            this.splitMain.Panel2.Controls.Add(this.lstHistorial);
            this.splitMain.Panel2.Controls.Add(this.lblHistorial);

            //
            // lblContactos
            //
            this.lblContactos.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblContactos.Name = "lblContactos";
            this.lblContactos.Size = new System.Drawing.Size(212, 22);
            this.lblContactos.Text = "CONTACTOS";
            this.lblContactos.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            //
            // lstContactos
            //
            this.lstContactos.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstContactos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstContactos.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.lstContactos.IntegralHeight = false;
            this.lstContactos.ItemHeight = 40;
            // Permite hablarle a varios a la vez con Ctrl o Mayús.
            this.lstContactos.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lstContactos.Name = "lstContactos";
            this.lstContactos.TabIndex = 1;
            this.lstContactos.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.lstContactos_DrawItem);
            this.lstContactos.SelectedIndexChanged += new System.EventHandler(this.lstContactos_SelectedIndexChanged);

            //
            // lblHistorial
            //
            this.lblHistorial.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblHistorial.Name = "lblHistorial";
            this.lblHistorial.Size = new System.Drawing.Size(548, 22);
            this.lblHistorial.Text = "HISTORIAL";
            this.lblHistorial.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            //
            // lstHistorial
            //
            this.lstHistorial.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstHistorial.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstHistorial.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.lstHistorial.IntegralHeight = false;
            this.lstHistorial.ItemHeight = 34;
            this.lstHistorial.Name = "lstHistorial";
            this.lstHistorial.TabIndex = 2;
            this.lstHistorial.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.lstHistorial_DrawItem);
            this.lstHistorial.DoubleClick += new System.EventHandler(this.lstHistorial_DoubleClick);
            this.lstHistorial.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lstHistorial_KeyDown);

            //
            // bottomPanel
            //
            this.bottomPanel.BackColor = System.Drawing.Color.FromArgb(24, 25, 28);
            this.bottomPanel.Controls.Add(this.btnRecord);
            this.bottomPanel.Controls.Add(this.btnPlay);
            this.bottomPanel.Controls.Add(this.vuMeter);
            this.bottomPanel.Controls.Add(this.lblMicHint);
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Location = new System.Drawing.Point(0, 386);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(800, 110);
            this.bottomPanel.TabIndex = 2;

            //
            // lblMicHint
            //
            this.lblMicHint.AutoSize = true;
            this.lblMicHint.Location = new System.Drawing.Point(14, 8);
            this.lblMicHint.Name = "lblMicHint";
            this.lblMicHint.Text = "Micrófono";

            //
            // vuMeter
            //
            this.vuMeter.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.vuMeter.Location = new System.Drawing.Point(14, 26);
            this.vuMeter.Name = "vuMeter";
            this.vuMeter.Size = new System.Drawing.Size(772, 10);
            this.vuMeter.TabStop = false;

            //
            // btnRecord
            //
            this.btnRecord.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.btnRecord.Location = new System.Drawing.Point(14, 46);
            this.btnRecord.Name = "btnRecord";
            this.btnRecord.Size = new System.Drawing.Size(680, 50);
            this.btnRecord.TabIndex = 3;
            this.btnRecord.Text = "MANTÉN PULSADO PARA HABLAR (F7)";
            this.btnRecord.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnRecord_MouseDown);
            this.btnRecord.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnRecord_MouseUp);

            //
            // btnPlay
            //
            this.btnPlay.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnPlay.Location = new System.Drawing.Point(702, 46);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(84, 50);
            this.btnPlay.TabIndex = 4;
            this.btnPlay.Text = "▶";
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);

            //
            // statusPanel
            //
            this.statusPanel.BackColor = System.Drawing.Color.FromArgb(20, 21, 24);
            this.statusPanel.Controls.Add(this.lblStatus);
            this.statusPanel.Controls.Add(this.lblAir);
            this.statusPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.statusPanel.Location = new System.Drawing.Point(0, 496);
            this.statusPanel.Name = "statusPanel";
            this.statusPanel.Size = new System.Drawing.Size(800, 26);
            this.statusPanel.TabIndex = 5;

            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(14, 5);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Text = "Listo";

            //
            // lblAir
            //
            this.lblAir.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.lblAir.Location = new System.Drawing.Point(660, 4);
            this.lblAir.Name = "lblAir";
            this.lblAir.Size = new System.Drawing.Size(126, 18);
            this.lblAir.Text = "";
            this.lblAir.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            //
            // trayIcon
            //
            this.trayIcon.ContextMenuStrip = this.trayMenu;
            this.trayIcon.Text = "Walkie Talkie";
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += new System.EventHandler(this.trayIcon_DoubleClick);

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 522);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.bottomPanel);
            this.Controls.Add(this.statusPanel);
            this.Controls.Add(this.topPanel);
            this.MinimumSize = new System.Drawing.Size(660, 500);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Walkie Talkie";

            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picUser)).EndInit();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.statusPanel.ResumeLayout(false);
            this.statusPanel.PerformLayout();
            this.ResumeLayout(false);
        }
        #endregion
    }
}
