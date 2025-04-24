﻿using System;
using System.Windows.Forms;
using Npgsql;

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
            get { return cbxShowPwd.Checked; }
            set { cbxShowPwd.Checked = value; }
        }

        public string Password
        {
            get { return txtPassword.Text.Trim(); }
            set { txtPassword.Text = value; }
        }
        public string Username
        {
            get { return txtUsername.Text.Trim(); }
            set { txtUsername.Text = value; }
        }
        public string Port
        {
            get { return nmrPort.Value.ToString(); }
            set { nmrPort.Value = decimal.Parse(value); }
        }
        public string Server
        {
            get { return txtServer.Text.Trim(); }
            set { txtServer.Text = value; }
        }
        public string Database
        {
            get { return txtDatabase.Text.Trim(); }
            set { txtDatabase.Text = value; }
        }
        public string ConnectionName
        {
            get { return txtConnName.Text.Trim(); }
            set { txtConnName.Text = value; }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        private void btnTestConnection_Click(object sender, EventArgs e)
        {
            var connection = new NpgsqlConnection();
            var builder = new NpgsqlConnectionStringBuilder
            {
                Username = Username,
                Host = Server,
                Port = int.Parse(Port),
                Database = Database,
                Password = Password,
            };
            connection.ConnectionString = builder.ConnectionString;
            try
            {
                connection.Open();
                MessageBox.Show(@"Connection successful", @"Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Unable to connect to server {ex.Message}", @"Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally 
            {
                connection.Close();
                connection.Dispose();
                NpgsqlConnection.ClearPool(connection);
            }
        }

        private void cbxShowPwd_CheckedChanged(object sender, EventArgs e)
        {
            AdjustPasswordChar();
        }

        private void AdjustPasswordChar()
        {
            txtPassword.PasswordChar = cbxShowPwd.Checked ? (char)0 : '*';
        }

        private void frmPassword_Load(object sender, EventArgs e)
        {
            AdjustPasswordChar();
        }

        public void SetConnNameVisible(bool visible)
        {
            lblConnName.Visible = visible;
            txtConnName.Visible = visible;
        }

        public void FocusPassword()
        {
            txtPassword.TabIndex = 0;
            btnOK.TabIndex = 1;
            btnCancel.TabIndex = 2;
            btnTestConnection.TabIndex = 3;
        }
    }
}
