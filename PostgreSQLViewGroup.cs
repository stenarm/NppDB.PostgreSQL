﻿using System.Windows.Forms;
using Npgsql;

namespace NppDB.PostgreSQL
{
    internal class PostgreSQLViewGroup : PostgreSqlTableGroup
    {
        public PostgreSQLViewGroup()
        {
            Query = "SELECT table_name " +
                "FROM information_schema.tables tbls " +
                "WHERE table_schema='{0}' " +
                "AND table_type='VIEW' " +
                "ORDER BY table_name;";
            Text = "Views";
        }

        protected override TreeNode CreateTreeNode(NpgsqlDataReader reader)
        {
            return new PostgreSQLView
            {
                Text = reader["table_name"].ToString()
                //Definition = dataRow["view_definition"].ToString(),
            };
        }
    }
}
