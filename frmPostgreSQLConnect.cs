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

        public string Password
        {
            get { return "password"; } // this.txtPassword.Text.Trim()
            set { this.txtPassword.Text = value; }
        }
        public string Username
        {
            get { return "username"; } // this.txtUsername.Text.Trim()
            set { this.txtUsername.Text = value; }
        }
        public string Port
        {
            get { return "15432"; } // this.txtPort.Text.Trim()
            set { this.txtPort.Text = value; }
        }
        public string Server
        {
            get { return "127.0.0.1"; } // this.txtServer.Text.Trim()
            set { this.txtServer.Text = value; }
        }
        public string Database
        {
            get { return "dvdrental"; } // this.txtServer.Text.Trim()
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
    }
}
