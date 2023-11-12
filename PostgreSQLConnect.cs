using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;
using Npgsql;
using NppDB.Comm;

namespace NppDB.PostgreSQL
{
    [XmlRoot]
    [ConnectAttr(Id = "PostgreSQLConnect", Title = "PostgreSQL")]
    public class PostgreSQLConnect : TreeNode, IDBConnect, IRefreshable, IMenuProvider, IIconProvider, INppDBCommandClient
    {
        [XmlElement]
        public string Title { set => Text = value; get => Text; }
        [XmlElement]
        public string ServerAddress { set; get; }
        public string Account { set; get; }
        public string Port { set; get; }
        public string Database { set; get; }
        public string ConnectionName { set; get; }
        public bool SaveConnectionDetails { set; get; }
        [XmlIgnore]
        public string Password { set; get; }
        private NpgsqlConnection _connection;

        public PostgreSQLConnect()
        {
            AppContext.SetSwitch("Npgsql.EnableSqlRewriting", false);
        }

        public bool IsOpened => _connection != null && _connection.State == ConnectionState.Open;

        internal INppDBCommandHost CommandHost { get; private set; }

        public void SetCommandHost(INppDBCommandHost host)
        {
            CommandHost = host;
        }

        public void Attach()
        {
            var id = CommandHost.Execute(NppDBCommandType.GetAttachedBufferID, null);
            if (id != null)
            {
                CommandHost.Execute(NppDBCommandType.NewFile, null);
            }
            id = CommandHost.Execute(NppDBCommandType.GetActivatedBufferID, null);
            CommandHost.Execute(NppDBCommandType.CreateResultView, new[] { id, this, CreateSQLExecutor() });
        }

        public ISQLExecutor CreateSQLExecutor()
        {
            return new PostgreSQLExecutor(GetConnection);
        }

        internal NpgsqlConnection GetConnection()
        {
            NpgsqlConnectionStringBuilder builder = GetConnectionStringBuilder();
            builder["Pwd"] = Password;
            return new NpgsqlConnection(builder.ConnectionString);
        }

        public bool CheckLogin()
        {
            var dlg = new frmPostgreSQLConnect { VisiblePassword = false };
            if (!String.IsNullOrEmpty(Account) || !String.IsNullOrEmpty(Port) || 
                !String.IsNullOrEmpty(ServerAddress) || !String.IsNullOrEmpty(Database) || !String.IsNullOrEmpty(Database)) 
            {
                dlg.Username = Account;
                dlg.Port = Port;
                dlg.Server = ServerAddress;
                dlg.Database = Database;
                dlg.ConnectionName = ConnectionName;
                dlg.SetConnNameVisible(false);
            }
            if (dlg.ShowDialog() != DialogResult.OK) return false;
            SaveConnectionDetails = dlg.SaveConnectionDetails;
            Password = dlg.Password;
            Account = dlg.Username;
            Port = dlg.Port;
            ServerAddress = dlg.Server;
            Database = dlg.Database;
            ConnectionName = dlg.ConnectionName;
            return true;
        }

        public void Connect()
        {
            if (_connection == null) _connection = new NpgsqlConnection();

            var curConnStrBuilder = GetConnectionStringBuilder();
            if (String.IsNullOrEmpty(_connection.ConnectionString) ||
                _connection.ConnectionString.Remove(_connection.ConnectionString.Length - 1, 1) != curConnStrBuilder.ConnectionString)
            {
                curConnStrBuilder["Password"] = Password;
                _connection.ConnectionString = curConnStrBuilder.ConnectionString;
            }
            if (_connection.State == ConnectionState.Open) return;
            try
            {
                _connection.Open();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("connect fail", ex);
            }
        }

        internal NpgsqlConnectionStringBuilder GetConnectionStringBuilder()
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Username = Account,
                Host = ServerAddress,
                Port = int.Parse(Port.ToString()),
                Database = Database
            };
            return builder;
        }

        public void ConnectAndAttach()
        {
            if (IsOpened || !CheckLogin()) return;
            try
            {
                Connect();
                Attach();
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : ""));
            }
        }

        public void Disconnect()
        {
            if (_connection == null || _connection.State == ConnectionState.Closed) return;

            _connection.Close();
        }

        public string GetDefaultTitle()
        {
            return !string.IsNullOrEmpty(ConnectionName) ? ConnectionName : !string.IsNullOrEmpty(Database) ? Database : "Untitled";
        }

        public Bitmap GetIcon()
        {
            return Properties.Resources.PostgreSQL;
        }

        public ContextMenuStrip GetMenu()
        {
            var menuList = new ContextMenuStrip { ShowImageMargin = false };
            var connect = this;
            var host = CommandHost;
            if (host != null)
            {
                menuList.Items.Add(new ToolStripButton("Open", null, (s, e) =>
                {
                    host.Execute(NppDBCommandType.NewFile, null);
                    var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                    host.Execute(NppDBCommandType.CreateResultView, new[] { id, connect, CreateSQLExecutor() });
                }));
                if (host.Execute(NppDBCommandType.GetAttachedBufferID, null) == null)
                {
                    menuList.Items.Add(new ToolStripButton("Attach", null, (s, e) =>
                    {
                        var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                        host.Execute(NppDBCommandType.CreateResultView, new[] { id, connect, CreateSQLExecutor() });
                    }));
                }
                else
                {
                    menuList.Items.Add(new ToolStripButton("Detach", null, (s, e) =>
                    {
                        host.Execute(NppDBCommandType.DestroyResultView, null);
                    }));
                }
                menuList.Items.Add(new ToolStripSeparator());
            }
            menuList.Items.Add(new ToolStripButton("Refresh", null, (s, e) => { Refresh(); }));
            return menuList;
        }

        public void Refresh()
        {
            using (var conn = GetConnection())
            {
                TreeView.Cursor = Cursors.WaitCursor;
                TreeView.Enabled = false;
                try
                {
                    conn.Open();
                    Nodes.Clear();
                    AddSchemas(conn);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Nodes.Clear();
                }
                finally
                {
                    conn.Close();
                    TreeView.Enabled = true;
                    TreeView.Cursor = null;
                }
            }
        }

        internal List<String> GetForeignSchemas(NpgsqlConnection conn)
        {
            List<String> result = new List<String>();
            string query = "SELECT DISTINCT foreign_table_schema FROM information_schema.foreign_tables;";
            using (NpgsqlCommand command = new NpgsqlCommand(query, conn))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string schemaName = reader["foreign_table_schema"].ToString();
                        result.Add(schemaName);
                    }
                }
            }
            return result;
        }

        internal void AddSchemas(NpgsqlConnection conn) 
        {
            string query = "SELECT nspname FROM pg_namespace order by nspname;";
            List<string> foreignSchemas = GetForeignSchemas(conn);
            using (NpgsqlCommand command = new NpgsqlCommand(query, conn))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string schemaName = reader["nspname"].ToString();
                        var db = new PostgreSQLSchema { Text = schemaName, Schema = schemaName, Foreign = foreignSchemas.Contains(schemaName) };
                        Nodes.Add(db);
                    }
                }
            }
        }

        public void Reset()
        {
            if (!SaveConnectionDetails) 
            {
                Database = ""; ServerAddress = ""; Account = "";; Port = "";
            }
            Password = "";
            Disconnect();
            _connection = null;
        }
    }
}
