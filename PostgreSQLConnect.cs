using System;
using System.Data;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
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
        [XmlIgnore]
        public string Password { set; get; }
        private OdbcConnection _connection;

        public bool IsOpened => _connection != null && _connection.State == ConnectionState.Open;

        internal INppDBCommandHost CommandHost { get; private set; }

        public void SetCommandHost(INppDBCommandHost host)
        {
            CommandHost = host;
        }

        public void Attach()
        {
            Console.WriteLine("start attach");
            var id = CommandHost.Execute(NppDBCommandType.GetAttachedBufferID, null);
            if (id != null)
            {
                CommandHost.Execute(NppDBCommandType.NewFile, null);
            }
            id = CommandHost.Execute(NppDBCommandType.GetActivatedBufferID, null);
            CommandHost.Execute(NppDBCommandType.CreateResultView, new[] { id, this, CreateSQLExecutor() });
            Console.WriteLine("end attach");
        }

        internal ISQLExecutor CreateSQLExecutor()
        {
            return new PostgreSQLExecutor(GetConnection);
        }

        internal OdbcConnection GetConnection()
        {
            return new OdbcConnection(GetConnectionString());
        }

        public bool CheckLogin()
        {
            var dlg = new frmPostgreSQLConnect();
            if (dlg.ShowDialog() != DialogResult.OK) return false;
            Password = dlg.Password;
            Account = dlg.Username;
            Port = dlg.Port;
            ServerAddress = dlg.Server;
            Database = dlg.Database;

            Console.WriteLine(Password);
            Console.WriteLine(Account);
            Console.WriteLine(Port);
            Console.WriteLine(ServerAddress);
            Console.WriteLine(Database);
            return true;
        }

        public void Connect()
        {
            if (_connection == null) _connection = new OdbcConnection();
            var curConnStr = GetConnectionString();
            if (_connection.ConnectionString != curConnStr) _connection.ConnectionString = curConnStr;
            if (_connection.State == ConnectionState.Open) return;
            try
            {
                _connection.Open();
                Console.WriteLine("connected?");
            }
            catch (Exception ex)
            {
                throw new ApplicationException("connect fail", ex);
            }
        }

        internal string GetConnectionString()
        {
            var builder = new OdbcConnectionStringBuilder
            {
                Driver = "PostgreSQL Unicode(x64)",
            };
            builder["Pwd"] = Password;
            builder["Uid"] = Account;
            builder["Server"] = ServerAddress;
            builder["Port"] = Port;
            builder["Database"] = Database;
            Console.WriteLine(builder.ConnectionString);
            return builder.ConnectionString;
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
            return string.IsNullOrEmpty(Database) ? "" : Database;
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
            Console.WriteLine("start Refresh");
            using (var conn = GetConnection())
            {
                TreeView.Cursor = Cursors.WaitCursor;
                TreeView.Enabled = false;
                try
                {
                    Console.WriteLine("Refresh conn.open");
                    conn.Open();
                    Console.WriteLine("Refresh conn.opened");
                    // OdbcMetaDataCollectionNames.


                    DataTable metaDataTable = conn.GetSchema("MetaDataCollections");
                    Console.WriteLine("Meta Data for Supported Schema Collections:");
                    ShowDataTable(metaDataTable, 25);
                    Console.WriteLine();

                    //// Get the schema information of Schemata in your instance
                    //// Retrieve a list of schemas
                    //string query = "SELECT nspname FROM pg_namespace;";

                    //using (OdbcCommand command = new OdbcCommand(query, conn))
                    //{
                    //    using (OdbcDataReader reader = command.ExecuteReader())
                    //    {
                    //        while (reader.Read())
                    //        {
                    //            string schemaName = reader["nspname"].ToString();
                    //            Console.WriteLine("Schema Name: " + schemaName);
                    //        }
                    //    }
                    //}

                    //// First, get schema information of all the tables in current database;
                    //DataTable allTablesSchemaTable = conn.GetSchema("Tables");

                    //Console.WriteLine("Schema Information of All Tables:");
                    //ShowDataTable(allTablesSchemaTable, 20);
                    //Console.WriteLine();


                    //// First, get schema information of all the columns in current database.
                    //DataTable allColumnsSchemaTable = conn.GetSchema("Columns");

                    //Console.WriteLine("Schema Information of All Columns:");
                    //ShowColumns(allColumnsSchemaTable);
                    //Console.WriteLine();

                    //var dt = conn.GetSchema(OdbcMetaDataCollectionNames.Tables);
                    //Console.WriteLine(dt);
                    Nodes.Clear();
                    string query = "SELECT nspname FROM pg_namespace;";
                    using (OdbcCommand command = new OdbcCommand(query, conn))
                    {
                        using (OdbcDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string schemaName = reader["nspname"].ToString();
                                var db = new PostgreSQLDatabase { Text = schemaName };
                                Nodes.Add(db);
                                Console.WriteLine("Schema Name: " + schemaName);
                            }
                        }
                    }
                    //foreach (DataRow row in dt.Rows)
                    //{
                    //    if (row["TABLE_TYPE"].ToString() != "SYSTEM TABLE")
                    //    {
                    //        var db = new PostgreSQLDatabase { Text = row["TABLE_NAME"].ToString() };
                    //        Nodes.Add(db);
                    //    }
                    //}
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Nodes.Clear();
                    Nodes.Add(new PostgreSQLDatabase { Text = "default" });
                }
                finally
                {
                    TreeView.Enabled = true;
                    TreeView.Cursor = null;
                }
            }
            Console.WriteLine("end Refresh");
        }

        private static void ShowDataTable(DataTable table, Int32 length)
        {
            foreach (DataColumn col in table.Columns)
            {
                Console.Write("{0,-" + length + "}", col.ColumnName);
            }
            Console.WriteLine();

            foreach (DataRow row in table.Rows)
            {
                foreach (DataColumn col in table.Columns)
                {
                    if (col.DataType.Equals(typeof(DateTime)))
                        Console.Write("{0,-" + length + ":d}", row[col]);
                    else if (col.DataType.Equals(typeof(Decimal)))
                        Console.Write("{0,-" + length + ":C}", row[col]);
                    else
                        Console.Write("{0,-" + length + "}", row[col]);
                }
                Console.WriteLine();
            }
        }

        private static void ShowDataTable(DataTable table)
        {
            ShowDataTable(table, 14);
        }

        private static void ShowColumns(DataTable columnsTable)
        {
            var selectedRows = from info in columnsTable.AsEnumerable()
                               select new
                               {
                                   TableCatalog = info["TABLE_CATALOG"],
                                   TableSchema = info["TABLE_SCHEMA"],
                                   TableName = info["TABLE_NAME"],
                                   ColumnName = info["COLUMN_NAME"],
                                   DataType = info["DATA_TYPE"]
                               };

            Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}{4,-15}", "TableCatalog", "TABLE_SCHEMA",
                "TABLE_NAME", "COLUMN_NAME", "DATA_TYPE");
            foreach (var row in selectedRows)
            {
                Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}{4,-15}", row.TableCatalog,
                    row.TableSchema, row.TableName, row.ColumnName, row.DataType);
            }
        }

        private static void ShowIndexColumns(DataTable indexColumnsTable)
        {
            var selectedRows = from info in indexColumnsTable.AsEnumerable()
                               select new
                               {
                                   TableSchema = info["table_schema"],
                                   TableName = info["table_name"],
                                   ColumnName = info["column_name"],
                                   ConstraintSchema = info["constraint_schema"],
                                   ConstraintName = info["constraint_name"],
                                   KeyType = info["KeyType"]
                               };

            Console.WriteLine("{0,-14}{1,-11}{2,-14}{3,-18}{4,-16}{5,-8}", "table_schema", "table_name", "column_name", "constraint_schema", "constraint_name", "KeyType");
            foreach (var row in selectedRows)
            {
                Console.WriteLine("{0,-14}{1,-11}{2,-14}{3,-18}{4,-16}{5,-8}", row.TableSchema,
                    row.TableName, row.ColumnName, row.ConstraintSchema, row.ConstraintName, row.KeyType);
            }
        }

        public void Reset()
        {
            Title = ""; ServerAddress = ""; Account = ""; Password = "";
            Disconnect();
            _connection = null;
        }
    }
}
