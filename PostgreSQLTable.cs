﻿using NppDB.Comm;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data;
using System.Windows.Forms;
using System.Data.Odbc;
using System.Collections;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Linq;
using System.Data.Common;
using System.Xml.Linq;
using static Antlr4.Runtime.Atn.SemanticContext;

namespace NppDB.PostgreSQL
{
    public class PostgreSQLTable : TreeNode, IRefreshable, IMenuProvider
    {
        public string Definition { get; set; }
        protected string TypeName { get; set; } = "TABLE";
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

                    Console.WriteLine("table conn open");
                    cnn.Open();
                    Nodes.Clear();

                    var columns = new List<PostgreSQLColumnInfo>();

                    var primaryKeyColumnNames = CollectPrimaryKeys(cnn, ref columns);
                    var foreignKeyColumnNames = CollectForeignKeys(cnn, ref columns);

                    Console.WriteLine("CollectForeignKeys");
                    foreach (var column in foreignKeyColumnNames) {
                        Console.WriteLine(column);
                    }
                    Console.WriteLine();

                    Console.WriteLine("CollectPrimaryKeys");
                    foreach (var column in primaryKeyColumnNames)
                    {
                        Console.WriteLine(column);
                    }
                    Console.WriteLine();
                    //var indexedColumnNames = CollectIndices(cnn, ref columns);

                    var columnCount = CollectColumns(cnn, ref columns, primaryKeyColumnNames, foreignKeyColumnNames);
                    if (columnCount == 0) return;

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
                    TreeView.Enabled = true;
                    TreeView.Cursor = null;
                }
            }
        }

        private int CollectColumns(OdbcConnection connection, ref List<PostgreSQLColumnInfo> columns,
            in List<string> primaryKeyColumnNames,
            in List<string> foreignKeyColumnNames
            //in List<string> indexedColumnNames
            )
        {
            //var dt = connection.GetSchema(OdbcMetaDataCollectionNames.Columns, new[] { null, null, Text, null });

            var count = 0;
            String query = "SELECT * FROM information_schema.columns WHERE table_schema = '{0}' AND table_name = '{1}' ORDER BY ordinal_position;";
            using (OdbcCommand command = new OdbcCommand(String.Format(query, Parent.Parent.Text, Text), connection))
            {
                using (OdbcDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader["column_name"].ToString();
                        var dataTypeName = getDataTypeName(reader);

                        var options = 0;

                        if (reader["is_nullable"].ToString() == "YES") options += 1;
                        //if (indexedColumnNames.Contains(columnName)) options += 10;
                        if (primaryKeyColumnNames.Contains(columnName)) options += 100;
                        if (foreignKeyColumnNames.Contains(columnName)) options += 1000;

                        columns.Insert(count++, new PostgreSQLColumnInfo(columnName, dataTypeName, 0, options));
                    }
                }
            }
            //foreach (DataRow row in dt.AsEnumerable().OrderBy(r => r["ordinal_position"]))
            //{
               
            //    var typename = ((OdbcType)Convert.ToInt32(row["data_type"])).ToString().ToUpper();
            //    var columnName = row["COLUMN_NAME"].ToString();
            //    var columnType = $"{typename}{(row["COLUMN_SIZE"] is DBNull ? "" : "(" + row["COLUMN_SIZE"] + ")")}";

            //    var options = 0;
            //    Console.WriteLine("typename: " + typename + " actual:" + Convert.ToInt32(row["data_type"]).ToString());
            //    Console.WriteLine("columnName: " + columnName);
            //    Console.WriteLine("columnType: " + columnType);
            //    Console.WriteLine("isNUllble: " + row["IS_NULLABLE"].ToString());

            //    if (!String.IsNullOrEmpty(row["IS_NULLABLE"].ToString()) && !Convert.ToBoolean(row["IS_NULLABLE"].ToString())) options += 1;
            //    //if (indexedColumnNames.Contains(columnName)) options += 10;
            //    if (primaryKeyColumnNames.Contains(columnName)) options += 100;
            //    if (foreignKeyColumnNames.Contains(columnName)) options += 1000;

            //    columns.Insert(count++, new PostgreSQLColumnInfo(columnName, columnType, 0, options));
            //}
            return count;
        }

        private string getDataTypeName(OdbcDataReader reader)
        {
            var dataType = reader["data_type"].ToString();
            var charMaxLen = reader["character_maximum_length"].ToString();
            var numericPrecision = reader["numeric_precision"].ToString();
            var numericScale = reader["numeric_scale"].ToString();
            if (!String.IsNullOrEmpty(charMaxLen))
            {
                dataType += $"({charMaxLen})";
            } else if (!String.IsNullOrEmpty(numericPrecision))
            {
                dataType += $"({numericPrecision}";
                if (!String.IsNullOrEmpty(numericScale))
                {
                    dataType += $",{numericScale}";
                }
                dataType += ")";
            }
            return dataType;
        }

        private List<string> CollectPrimaryKeys(OdbcConnection connection, ref List<PostgreSQLColumnInfo> columns)
        {
            var query = "SELECT distinct conname as constraint_name " +
                ", pg_get_constraintdef(oid) as constraint_definition " +
                "FROM pg_constraint " +
                "JOIN pg_attribute a ON a.attrelid = conrelid " +
                "WHERE contype IN('p') " +
                "AND connamespace = '{0}'::regnamespace " +
                "AND conrelid = '{1}'::regclass";

            var names = new List<string>();
            using (OdbcCommand command = new OdbcCommand(String.Format(query, Parent.Parent.Text, Text), connection))
            {
                using (OdbcDataReader reader = command.ExecuteReader())
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

        private List<string> CollectForeignKeys(OdbcConnection connection, ref List<PostgreSQLColumnInfo> columns)
        {
            var query = "SELECT distinct conname as constraint_name " +
                ", pg_get_constraintdef(oid) as constraint_definition " +
                "FROM pg_constraint " +
                "JOIN pg_attribute a ON a.attrelid = conrelid " +
                "WHERE contype IN('f') " +
                "AND connamespace = '{0}'::regnamespace " +
                "AND conrelid = '{1}'::regclass"; ;

            var names = new List<string>();

            using (OdbcCommand command = new OdbcCommand(String.Format(query, Parent.Parent.Text, Text), connection))
            {
                using (OdbcDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var foreignKeyName = reader["constraint_name"].ToString();
                        var foreignKeyDef = reader["constraint_definition"].ToString();
                        Console.WriteLine(foreignKeyName);
                        Console.WriteLine(foreignKeyDef);
                        var foreignKeyDefFormatted = reader["constraint_definition"].ToString();
                        Match fkColumnNameMatch = Regex.Match(foreignKeyDef, @"FOREIGN KEY \((?s)(.*)\) REFERENCES", RegexOptions.IgnoreCase);
                        Match fkTargetMatch = Regex.Match(foreignKeyDef, @"REFERENCES (?s)(.*)", RegexOptions.IgnoreCase);

                        if (fkColumnNameMatch.Success)
                        {
                            Console.WriteLine("fkColumnNameMatch");
                        }
                        if (fkTargetMatch.Success)
                        {
                            Console.WriteLine("fkTargetMatch");
                        }
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

        public ContextMenuStrip GetMenu()
        {
            throw new NotImplementedException();
        }

        private PostgreSQLConnect GetDBConnect()
        {
            var connect = Parent.Parent.Parent as PostgreSQLConnect;
            return connect;
        }
    }
}
