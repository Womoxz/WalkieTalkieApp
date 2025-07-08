// MainForm.Designer.cs
namespace WalkieTalkieApp
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer? components = null;

        // Declaración de controles para que sean accesibles desde MainForm.cs
        internal System.Windows.Forms.Panel topPanel;
        internal System.Windows.Forms.PictureBox picUser;
        internal System.Windows.Forms.Label lblUserName;
        internal System.Windows.Forms.Label lblContactos;
        internal System.Windows.Forms.ComboBox cmbContactos;
        internal System.Windows.Forms.Label lblHistorial;
        internal System.Windows.Forms.ListBox lstHistorial;
        internal System.Windows.Forms.Button btnRecord;
        internal System.Windows.Forms.Button btnPlay;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.topPanel = new System.Windows.Forms.Panel();
            this.picUser = new System.Windows.Forms.PictureBox();
            this.lblUserName = new System.Windows.Forms.Label();
            this.lblContactos = new System.Windows.Forms.Label();
            this.cmbContactos = new System.Windows.Forms.ComboBox();
            this.lblHistorial = new System.Windows.Forms.Label();
            this.lstHistorial = new System.Windows.Forms.ListBox();
            this.btnRecord = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picUser)).BeginInit();
            this.SuspendLayout();
            // 
            // topPanel
            // 
            this.topPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(37)))), ((int)(((byte)(38)))));
            this.topPanel.Controls.Add(this.picUser);
            this.topPanel.Controls.Add(this.lblUserName);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Size = new System.Drawing.Size(384, 80);
            this.topPanel.TabIndex = 7;
            // 
            // picUser
            // 
            this.picUser.Location = new System.Drawing.Point(12, 12);
            this.picUser.Name = "picUser";
            this.picUser.Size = new System.Drawing.Size(60, 60);
            this.picUser.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picUser.TabIndex = 6;
            this.picUser.TabStop = false;
            // 
            // lblUserName
            // 
            this.lblUserName.AutoSize = true;
            this.lblUserName.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblUserName.ForeColor = System.Drawing.Color.White;
            this.lblUserName.Location = new System.Drawing.Point(80, 28);
            this.lblUserName.Name = "lblUserName";
            this.lblUserName.Size = new System.Drawing.Size(110, 25);
            this.lblUserName.TabIndex = 8;
            this.lblUserName.Text = "User Name";
            // 
            // lblContactos
            // 
            this.lblContactos.AutoSize = true;
            this.lblContactos.Font = new System.Drawing.Font("Segoe UI Semibold", 10F);
            this.lblContactos.Location = new System.Drawing.Point(12, 95);
            this.lblContactos.Name = "lblContactos";
            this.lblContactos.Size = new System.Drawing.Size(123, 19);
            this.lblContactos.TabIndex = 0;
            this.lblContactos.Text = "Hablar con:";
            // 
            // cmbContactos
            // 
            this.cmbContactos.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbContactos.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbContactos.FormattingEnabled = true;
            this.cmbContactos.Location = new System.Drawing.Point(16, 117);
            this.cmbContactos.Name = "cmbContactos";
            this.cmbContactos.Size = new System.Drawing.Size(356, 25);
            this.cmbContactos.TabIndex = 1;
            // 
            // lblHistorial
            // 
            this.lblHistorial.AutoSize = true;
            this.lblHistorial.Font = new System.Drawing.Font("Segoe UI Semibold", 10F);
            this.lblHistorial.Location = new System.Drawing.Point(12, 160);
            this.lblHistorial.Name = "lblHistorial";
            this.lblHistorial.Size = new System.Drawing.Size(69, 19);
            this.lblHistorial.TabIndex = 2;
            this.lblHistorial.Text = "Historial";
            // 
            // lstHistorial
            // 
            this.lstHistorial.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstHistorial.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lstHistorial.FormattingEnabled = true;
            this.lstHistorial.ItemHeight = 15;
            this.lstHistorial.Location = new System.Drawing.Point(16, 182);
            this.lstHistorial.Name = "lstHistorial";
            this.lstHistorial.Size = new System.Drawing.Size(356, 167);
            this.lstHistorial.TabIndex = 3;
            // 
            // btnRecord
            // 
            this.btnRecord.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRecord.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnRecord.Location = new System.Drawing.Point(16, 365);
            this.btnRecord.Name = "btnRecord";
            this.btnRecord.Size = new System.Drawing.Size(265, 45);
            this.btnRecord.TabIndex = 4;
            this.btnRecord.Text = "PRESIONAR PARA HABLAR (F7)";
            this.btnRecord.UseVisualStyleBackColor = true;
            this.btnRecord.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnRecord_MouseDown);
            this.btnRecord.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnRecord_MouseUp);
            // 
            // btnPlay
            // 
            this.btnPlay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPlay.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnPlay.Location = new System.Drawing.Point(287, 365);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(85, 45);
            this.btnPlay.TabIndex = 5;
            this.btnPlay.Text = "PLAY";
            this.btnPlay.UseVisualStyleBackColor = true;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 421);
            this.Controls.Add(this.topPanel);
            this.Controls.Add(this.btnPlay);
            this.Controls.Add(this.btnRecord);
            this.Controls.Add(this.lstHistorial);
            this.Controls.Add(this.lblHistorial);
            this.Controls.Add(this.cmbContactos);
            this.Controls.Add(this.lblContactos);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(400, 460);
            this.Name = "MainForm";
            this.Text = "Walkie Talkie";
            this.topPanel.ResumeLayout(false);
            this.topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picUser)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion
    }
}