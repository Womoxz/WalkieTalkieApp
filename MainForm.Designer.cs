namespace WalkieTalkieApp
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnRecord;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.ComboBox cmbContactos;
        private System.Windows.Forms.ListBox lstHistorial;
        private System.Windows.Forms.Label lblContactos;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cmbContactos = new System.Windows.Forms.ComboBox();
            this.btnRecord = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.lstHistorial = new System.Windows.Forms.ListBox();
            this.lblContactos = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cmbContactos
            // 
            this.cmbContactos.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbContactos.FormattingEnabled = true;
            this.cmbContactos.Location = new System.Drawing.Point(30, 30);
            this.cmbContactos.Name = "cmbContactos";
            this.cmbContactos.Size = new System.Drawing.Size(260, 21);
            this.cmbContactos.TabIndex = 0;
            // 
            // btnRecord
            // 
            this.btnRecord.Location = new System.Drawing.Point(30, 70);
            this.btnRecord.Name = "btnRecord";
            this.btnRecord.Size = new System.Drawing.Size(120, 40);
            this.btnRecord.TabIndex = 1;
            this.btnRecord.Text = "MANTENER PARA HABLAR";
            this.btnRecord.UseVisualStyleBackColor = true;
            this.btnRecord.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnRecord_MouseDown);
            this.btnRecord.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnRecord_MouseUp);
            // 
            // btnPlay
            // 
            this.btnPlay.Location = new System.Drawing.Point(170, 70);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(120, 40);
            this.btnPlay.TabIndex = 2;
            this.btnPlay.Text = "Reproducir";
            this.btnPlay.UseVisualStyleBackColor = true;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // lstHistorial
            // 
            this.lstHistorial.FormattingEnabled = true;
            this.lstHistorial.Location = new System.Drawing.Point(30, 130);
            this.lstHistorial.Name = "lstHistorial";
            this.lstHistorial.Size = new System.Drawing.Size(260, 160);
            this.lstHistorial.TabIndex = 3;
            // 
            // lblContactos
            // 
            this.lblContactos.AutoSize = true;
            this.lblContactos.Location = new System.Drawing.Point(30, 10);
            this.lblContactos.Name = "lblContactos";
            this.lblContactos.Size = new System.Drawing.Size(104, 13);
            this.lblContactos.TabIndex = 4;
            this.lblContactos.Text = "Seleccionar Contacto";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(320, 310);
            this.Controls.Add(this.lblContactos);
            this.Controls.Add(this.lstHistorial);
            this.Controls.Add(this.btnPlay);
            this.Controls.Add(this.btnRecord);
            this.Controls.Add(this.cmbContactos);
            this.Name = "MainForm";
            this.Text = "Walkie Talkie Empresarial";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}