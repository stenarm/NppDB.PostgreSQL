namespace NppDB.PostgreSQL
{
    partial class frmPostgreSQLConnect
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            this.txtServer = new System.Windows.Forms.TextBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.lblPort = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.txtDatabase = new System.Windows.Forms.TextBox();
            this.cbxShowPwd = new System.Windows.Forms.CheckBox();
            this.cbxSaveConnectionDetails = new System.Windows.Forms.CheckBox();
            this.nmrPort = new System.Windows.Forms.NumericUpDown();
            ((System.ComponentModel.ISupportInitialize)(this.nmrPort)).BeginInit();
            this.SuspendLayout();
            // 
            // txtServer
            // 
            this.txtServer.Location = new System.Drawing.Point(273, 45);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(225, 20);
            this.txtServer.TabIndex = 0;
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(186, 48);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(29, 13);
            this.lblServer.TabIndex = 1;
            this.lblServer.Text = "Host";
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(186, 84);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(26, 13);
            this.lblPort.TabIndex = 3;
            this.lblPort.Text = "Port";
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(186, 163);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(55, 13);
            this.lblUsername.TabIndex = 5;
            this.lblUsername.Text = "Username";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(273, 160);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(225, 20);
            this.txtUsername.TabIndex = 4;
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(187, 203);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(53, 13);
            this.lblPassword.TabIndex = 7;
            this.lblPassword.Text = "Password";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(273, 200);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(225, 20);
            this.txtPassword.TabIndex = 6;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(273, 265);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 8;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(422, 264);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(186, 121);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 13);
            this.label1.TabIndex = 11;
            this.label1.Text = "Database";
            // 
            // txtDatabase
            // 
            this.txtDatabase.Location = new System.Drawing.Point(273, 118);
            this.txtDatabase.Name = "txtDatabase";
            this.txtDatabase.Size = new System.Drawing.Size(225, 20);
            this.txtDatabase.TabIndex = 10;
            // 
            // cbxShowPwd
            // 
            this.cbxShowPwd.AutoSize = true;
            this.cbxShowPwd.Location = new System.Drawing.Point(504, 203);
            this.cbxShowPwd.Name = "cbxShowPwd";
            this.cbxShowPwd.Size = new System.Drawing.Size(101, 17);
            this.cbxShowPwd.TabIndex = 12;
            this.cbxShowPwd.Text = "Show password";
            this.cbxShowPwd.UseVisualStyleBackColor = true;
            this.cbxShowPwd.CheckedChanged += new System.EventHandler(this.cbxShowPwd_CheckedChanged);
            // 
            // cbxSaveConnectionDetails
            // 
            this.cbxSaveConnectionDetails.AutoSize = true;
            this.cbxSaveConnectionDetails.Location = new System.Drawing.Point(273, 242);
            this.cbxSaveConnectionDetails.Name = "cbxSaveConnectionDetails";
            this.cbxSaveConnectionDetails.Size = new System.Drawing.Size(140, 17);
            this.cbxSaveConnectionDetails.TabIndex = 13;
            this.cbxSaveConnectionDetails.Text = "Save connection details";
            this.cbxSaveConnectionDetails.UseVisualStyleBackColor = true;
            this.cbxSaveConnectionDetails.Visible = false;
            // 
            // nmrPort
            // 
            this.nmrPort.Location = new System.Drawing.Point(273, 82);
            this.nmrPort.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.nmrPort.Name = "nmrPort";
            this.nmrPort.Size = new System.Drawing.Size(225, 20);
            this.nmrPort.TabIndex = 14;
            this.nmrPort.Value = new decimal(new int[] {
            5432,
            0,
            0,
            0});
            // 
            // frmPostgreSQLConnect
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.nmrPort);
            this.Controls.Add(this.cbxSaveConnectionDetails);
            this.Controls.Add(this.cbxShowPwd);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtDatabase);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblPort);
            this.Controls.Add(this.lblServer);
            this.Controls.Add(this.txtServer);
            this.Name = "frmPostgreSQLConnect";
            this.Text = "PostgreSQL server connection";
            this.Load += new System.EventHandler(this.frmPassword_Load);
            ((System.ComponentModel.ISupportInitialize)(this.nmrPort)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtDatabase;
        private System.Windows.Forms.CheckBox cbxShowPwd;
        private System.Windows.Forms.CheckBox cbxSaveConnectionDetails;
        private System.Windows.Forms.NumericUpDown nmrPort;
    }
}