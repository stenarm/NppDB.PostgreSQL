using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NppDB.PostgreSQL
{
    public partial class frmPostgreSQLConnect : Form
    {
        public frmPostgreSQLConnect()
        {
            InitializeComponent();
        }

        public bool VisiblePassword
        {
            get { return this.cbxShowPwd.Checked; }
            set { this.cbxShowPwd.Checked = value; }
        }
        public bool SaveConnectionDetails
        {
            get { return this.cbxSaveConnectionDetails.Checked; }
            set { this.cbxSaveConnectionDetails.Checked = value; }
        }

        public string Password
        {
            get { return this.txtPassword.Text.Trim(); } // this.txtPassword.Text.Trim() "password"
            set { this.txtPassword.Text = value; }
        }
        public string Username
        {
            get { return this.txtUsername.Text.Trim(); } // this.txtUsername.Text.Trim() "username"
            set { this.txtUsername.Text = value; }
        }
        public string Port
        {
            get { return this.txtPort.Text.Trim(); } // this.txtPort.Text.Trim() "15432"
            set { this.txtPort.Text = value; }
        }
        public string Server
        {
            get { return this.txtServer.Text.Trim(); } // "127.0.0.1"
            set { this.txtServer.Text = value; }
        }
        public string Database
        {
            get { return this.txtDatabase.Text.Trim(); } // this.txtDatabase.Text.Trim() "dvdrental"
            set { this.txtDatabase.Text = value; }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }

        private void cbxShowPwd_CheckedChanged(object sender, EventArgs e)
        {
            AdjustPasswordChar();
        }

        private void AdjustPasswordChar()
        {
            this.txtPassword.PasswordChar = this.cbxShowPwd.Checked ? (char)0 : '*';
        }

        private void frmPassword_Load(object sender, EventArgs e)
        {
            AdjustPasswordChar();
        }
    }
}
