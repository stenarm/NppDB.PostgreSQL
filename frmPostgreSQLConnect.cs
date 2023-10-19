using System;
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
            get { return this.txtPassword.Text.Trim(); }
            set { this.txtPassword.Text = value; }
        }
        public string Username
        {
            get { return this.txtUsername.Text.Trim(); }
            set { this.txtUsername.Text = value; }
        }
        public string Port
        {
            get { return this.nmrPort.Value.ToString(); }
            set { this.nmrPort.Value = decimal.Parse(value); }
        }
        public string Server
        {
            get { return this.txtServer.Text.Trim(); }
            set { this.txtServer.Text = value; }
        }
        public string Database
        {
            get { return this.txtDatabase.Text.Trim(); }
            set { this.txtDatabase.Text = value; }
        }
        public string ConnectionName
        {
            get { return this.txtConnName.Text.Trim(); }
            set { this.txtConnName.Text = value; }
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

        public void SetConnNameVisible(bool visible)
        {
            this.descConnName.Visible = visible;
            this.lblConnName.Visible = visible;
            this.txtConnName.Visible = visible;
        }
    }
}
