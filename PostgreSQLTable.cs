﻿using NppDB.Comm;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;
using Npgsql;
using System.Collections;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;

namespace NppDB.PostgreSQL
{
    public class PostgreSQLTable : TreeNode, IRefreshable, IMenuProvider
    {
        public string Definition { get; set; }
        public string TypeName { get; set; } = "TABLE";
        public string FuncOID { get; set; }
        public PostgreSQLTable()
        {
            SelectedImageKey = ImageKey = "Table";
        }

        public void Refresh()
        {
            var conn = GetDBConnect();
            using (var cnn = conn.GetConnection())
            {
                TreeView.Enabled = false;
                TreeView.Cursor = Cursors.WaitCursor;
                try
                {
                    cnn.Open();
                    Nodes.Clear();

                    var columns = new List<PostgreSQLColumnInfo>();

                    if (GetSchema().Foreign)
                    {
                        var columnCount = CollectColumns(cnn, ref columns, new List<string>(), new List<string>(), new List<string>());
                        if (columnCount == 0) return;
                    }
                    else if (TypeName == "FUNCTION")
                    {
                        var columnCount = CollectFunctionColumns(cnn, ref columns);
                        if (columnCount == 0) return;
                    }
                    else 
                    {
                        var primaryKeyColumnNames = CollectPrimaryKeys(cnn, ref columns);
                        var foreignKeyColumnNames = CollectForeignKeys(cnn, ref columns);
                        var indexedColumnNames = CollectIndices(cnn, ref columns);

                        var columnCount = CollectColumns(cnn, ref columns, primaryKeyColumnNames, foreignKeyColumnNames, indexedColumnNames);
                        if (columnCount == 0) return;
                    }

                    var maxLength = columns.Max(c => c.ColumnName.Length);
                    columns.ForEach(c => c.AdjustColumnNameFixedWidth(maxLength));
                    Nodes.AddRange(columns.ToArray<TreeNode>());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Exception");
                }
                finally
                {
                    cnn.Close();
                    TreeView.Enabled = true;
                    TreeView.Cursor = null;
                }
            }
        }

        private int CollectFunctionColumns(NpgsqlConnection connection, ref List<PostgreSQLColumnInfo> columns)
        {
            var count = 0;
            String query = "select pg_get_function_arguments(p.oid) as function_arguments " +
                "from pg_proc p " +
                "left join pg_namespace n on p.pronamespace = n.oid " +
                "where n.nspname = '{0}' and p.proname = '{1}' and p.oid = '{2}'";
            using (NpgsqlCommand command = new NpgsqlCommand(String.Format(query, GetSchemaName(), Text, FuncOID), connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var functionArguments = reader["function_arguments"].ToString();
                        string[] functionArgumentsArray = functionArguments.Split(',');
                        for (int i = 0; i < functionArgumentsArray.Length; i++)
                        {
                            string functionArgument = functionArgumentsArray[i];
                            string[] argumentNameAndType = functionArgument.Trim().Split(' ');
                            if (argumentNameAndType.Length > 1) 
                            {
                                if (!string.IsNullOrEmpty(argumentNameAndType[0]) && !string.IsNullOrEmpty(argumentNameAndType[1]))
                                {
                                    PostgreSQLColumnInfo postgreSQLColumnInfo = new PostgreSQLColumnInfo(argumentNameAndType[0], argumentNameAndType[1].ToUpper(), 0, 0);
                                    columns.Insert(count++, postgreSQLColumnInfo);
                                }
                            }
                            else if (argumentNameAndType.Length == 1)
                            {
                                if (!string.IsNullOrEmpty(argumentNameAndType[0]))
                                {
                                    PostgreSQLColumnInfo postgreSQLColumnInfo = new PostgreSQLColumnInfo(argumentNameAndType[0].ToUpper(), "", 0, 0);
                                    columns.Insert(count++, postgreSQLColumnInfo);
                                }
                            }
                        }
                    }
                }
            }
            return count;
        }

        private int CollectColumns(NpgsqlConnection connection, ref List<PostgreSQLColumnInfo> columns,
            in List<string> primaryKeyColumnNames,
            in List<string> foreignKeyColumnNames,
            in List<string> indexedColumnNames
            )
        {
            var count = 0;
            String query = "SELECT attr.attname AS column_name, " +
                    "pg_catalog.format_type(attr.atttypid, attr.atttypmod) AS data_type, " +
                    "pg_catalog.pg_get_expr(d.adbin, d.adrelid) AS column_default, " +
                    "attr.attnotnull::TEXT AS is_nullable " +
                    "FROM pg_catalog.pg_attribute AS attr " +
                    "LEFT JOIN pg_catalog.pg_attrdef d ON (attr.attrelid, attr.attnum) = (d.adrelid, d.adnum) " +
                    "JOIN pg_catalog.pg_class AS cls ON cls.oid = attr.attrelid " +
                    "JOIN pg_catalog.pg_namespace AS ns ON ns.oid = cls.relnamespace " +
                    "JOIN pg_catalog.pg_type AS tp ON tp.oid = attr.atttypid " +
                    "WHERE ns.nspname = '{0}' " +
                    "AND cls.relname = '{1}' " +
                    "AND attr.attnum >= 1 " +
                    "ORDER BY attr.attnum";
            using (NpgsqlCommand command = new NpgsqlCommand(String.Format(query, GetSchemaName(), Text), connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["column_name"].ToString();
                        var dataTypeName = GetDataTypeName(reader);

                        var options = 0;

                        if (reader["is_nullable"].ToString() == "true") options += 1;
                        if (indexedColumnNames.Contains(columnName)) options += 10;
                        if (primaryKeyColumnNames.Contains(columnName)) options += 100;
                        if (foreignKeyColumnNames.Contains(columnName)) options += 1000;

                        columns.Insert(count++, new PostgreSQLColumnInfo(columnName, dataTypeName, 0, options));
                    }
                }
            }
            return count;
        }

        private string GetDataTypeName(NpgsqlDataReader reader)
        {
            var dataType = reader["data_type"].ToString();
            var columnDefault = reader["column_default"].ToString();
            if (!String.IsNullOrEmpty(columnDefault))
            {
                dataType += $" => {columnDefault}";
            }
            return dataType.ToUpper();
        }

        private List<string> CollectPrimaryKeys(NpgsqlConnection connection, ref List<PostgreSQLColumnInfo> columns)
        {
            var query = "SELECT distinct c.conname as constraint_name " +
                ", pg_get_constraintdef(c.oid) as constraint_definition " +
                "FROM pg_catalog.pg_constraint c " +
                "JOIN pg_catalog.pg_attribute a ON a.attrelid = c.conrelid " +
                "JOIN pg_catalog.pg_class AS cls ON cls.oid = a.attrelid " +
                "WHERE c.contype IN('p') " +
                "AND c.connamespace = '{0}'::regnamespace " +
                "AND cls.relname = '{1}'";

            var names = new List<string>();
            using (NpgsqlCommand command = new NpgsqlCommand(String.Format(query, GetSchemaName(), Text), connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var primaryKeyName = reader["constraint_name"].ToString();
                        var primaryKeyType = reader["constraint_definition"].ToString();
                        Match primaryKeyTargetMatch = Regex.Match(primaryKeyType, @"PRIMARY KEY \((?s)(.*)\)", RegexOptions.IgnoreCase);
                        if (primaryKeyTargetMatch.Success && primaryKeyTargetMatch.Groups.Count > 1)
                        {
                            names.Add(primaryKeyTargetMatch.Groups[1].ToString());
                        }
                        columns.Add(new PostgreSQLColumnInfo(primaryKeyName, primaryKeyType, 1, 0));
                    }
                }
            }
            return names;
        }

        private List<string> CollectForeignKeys(NpgsqlConnection connection, ref List<PostgreSQLColumnInfo> columns)
        {
            var query = "SELECT distinct c.conname as constraint_name " +
                ", pg_get_constraintdef(c.oid) as constraint_definition " +
                "FROM pg_catalog.pg_constraint c " +
                "JOIN pg_catalog.pg_attribute a ON a.attrelid = c.conrelid " +
                "JOIN pg_catalog.pg_class AS cls ON cls.oid = a.attrelid " +
                "WHERE c.contype IN('f') " +
                "AND c.connamespace = '{0}'::regnamespace " +
                "AND cls.relname = '{1}'"; ;

            var names = new List<string>();;
            using (NpgsqlCommand command = new NpgsqlCommand(String.Format(query, GetSchemaName(), Text), connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var foreignKeyName = reader["constraint_name"].ToString();
                        var foreignKeyDef = reader["constraint_definition"].ToString();

                        var foreignKeyDefFormatted = reader["constraint_definition"].ToString();
                        Match fkColumnNameMatch = Regex.Match(foreignKeyDef, @"FOREIGN KEY \((?s)(.*)\) REFERENCES", RegexOptions.IgnoreCase);
                        Match fkTargetMatch = Regex.Match(foreignKeyDef, @"REFERENCES (?s)(.*)", RegexOptions.IgnoreCase);
                        if (fkColumnNameMatch.Success && fkColumnNameMatch.Groups.Count > 1 && fkTargetMatch.Success && fkTargetMatch.Groups.Count > 1) 
                        {
                            foreignKeyDefFormatted = $"({fkColumnNameMatch.Groups[1]}) -> {fkTargetMatch.Groups[1]}";
                            names.Add(fkColumnNameMatch.Groups[1].ToString());
                        }
                        columns.Add(new PostgreSQLColumnInfo(foreignKeyName, foreignKeyDefFormatted, 2, 0));
                    }
                }
            }
            return names;
        }

        private List<string> CollectIndices(NpgsqlConnection connection, ref List<PostgreSQLColumnInfo> columns)
        {
            var query = "select * from pg_catalog.pg_indexes where schemaname = '{0}' and tablename = '{1}';";

            var names = new List<string>();
            using (NpgsqlCommand command = new NpgsqlCommand(String.Format(query, GetSchemaName(), Text), connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var indexName = reader["indexname"].ToString();
                        string indexDef = reader["indexdef"].ToString();

                        var indexDefFormatted = reader["indexdef"].ToString();
                        Match indexDefMatch = Regex.Match(indexDef, @"btree \((?s)(.*)\)", RegexOptions.IgnoreCase);
                        if (indexDefMatch.Success && indexDefMatch.Groups.Count > 1)
                        {
                            string indexColumns = indexDefMatch.Groups[1].ToString();
                            foreach (string column in indexColumns.Split(','))
                            {
                                names.Add(column.Trim());
                            }
                            indexDefFormatted = $"({indexColumns})";
                        }

                        columns.Add(new PostgreSQLColumnInfo(indexName, indexDefFormatted, indexDef?.IndexOf("unique", StringComparison.OrdinalIgnoreCase) >= 0 ? 4 : 3, 0));
                    }
                }
            }
            return names;
        }

        public virtual ContextMenuStrip GetMenu()
        {
            var menuList = new ContextMenuStrip { ShowImageMargin = false };
            var connect = GetDBConnect();
            menuList.Items.Add(new ToolStripButton("Refresh", null, (s, e) => { Refresh(); }));
            if (connect?.CommandHost == null) return menuList;
            menuList.Items.Add(new ToolStripSeparator());

            var host = connect.CommandHost;
            string schemaName = GetSchemaName();
            if (TypeName != "FUNCTION") 
            {
                menuList.Items.Add(new ToolStripButton($"SELECT * FROM {Text}", null, (s, e) =>
                {
                    host.Execute(NppDBCommandType.NewFile, null);
                    var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                    var query = $"SELECT * FROM \"{schemaName}\".\"{Text}\";";
                    host.Execute(NppDBCommandType.AppendToCurrentView, new object[] { query });
                    host.Execute(NppDBCommandType.CreateResultView, new[] { id, connect, connect.CreateSqlExecutor() });
                    host.Execute(NppDBCommandType.ExecuteSQL, new[] { id, query });
                }));
            }
            if (TypeName == "MATERIALIZED_VIEW")
            {
                menuList.Items.Add(new ToolStripButton($"REFRESH MATERIALIZED VIEW", null, (s, e) =>
                {
                    var query = $"REFRESH MATERIALIZED VIEW \"{Text}\";";
                    var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                    host.Execute(NppDBCommandType.ExecuteSQL, new[] { id, query });
                }));
                menuList.Items.Add(new ToolStripButton($"DROP MATERIALIZED VIEW", null, (s, e) =>
                {
                    var query = $"DROP MATERIALIZED VIEW \"{Text}\";";
                    var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                    host.Execute(NppDBCommandType.ExecuteSQL, new[] { id, query });
                }));
            }
            else if (TypeName != "FOREIGN_TABLE") 
            {
                if (schemaName != "information_schema" && schemaName != "pg_catalog")
                {
                    if (TypeName != "FUNCTION")
                    {
                        menuList.Items.Add(new ToolStripButton($"DROP {TypeName.ToUpper()}", null, (s, e) =>
                        {
                            var query = $"DROP {TypeName} \"{GetSchemaName()}\".\"{Text}\";";
                            var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                            host.Execute(NppDBCommandType.ExecuteSQL, new[] { id, query });
                        }));
                    }
                    else
                    {
                        menuList.Items.Add(new ToolStripButton($"DROP {TypeName.ToUpper()}", null, (s, e) =>
                        {
                            string paramsQuery = collectFunctionParams(connect);
                            var query = $"DROP {TypeName} \"{GetSchemaName()}\".\"{Text}\"{paramsQuery};";
                            var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
                            host.Execute(NppDBCommandType.ExecuteSQL, new[] { id, query });
                        }));
                    }
                }
            }
            // Needed an invisible button so previous buttons' text isn't cut off
            ToolStripButton dummy = new ToolStripButton("Dummy", null, (s, e) => { });
            dummy.Visible = false;
            menuList.Items.Add(dummy);
            return menuList;
        }

        private string collectFunctionParams(PostgreSqlConnect connect)
        {
            var paramsQuery = "()";
            using (var cnn = connect.GetConnection())
            {
                try
                {
                    cnn.Open();
                    var columns = new List<PostgreSQLColumnInfo>();
                    CollectFunctionColumns(cnn, ref columns);
                    if (columns.Count > 0)
                    {
                        paramsQuery = "(";
                        for (int i = 0; i < columns.Count; i++)
                        {
                            PostgreSQLColumnInfo column = columns[i];
                            if (column.ColumnType == "")
                            {
                                paramsQuery += column.ColumnName;
                            }
                            else
                            {
                                paramsQuery += column.ColumnType;
                            }
                            if (i + 1 < columns.Count)
                            {
                                paramsQuery += ",";
                            }
                        }
                        paramsQuery += ")";
                    }
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    cnn.Close();
                }
            }

            return paramsQuery;
        }

        private PostgreSqlConnect GetDBConnect()
        {
            var connect = Parent.Parent.Parent as PostgreSqlConnect;
            return connect;
        }

        private PostgreSQLSchema GetSchema()
        {
            return Parent.Parent as PostgreSQLSchema;
        }

        private string GetSchemaName()
        {
            return GetSchema().Schema;
        }
    }
}
