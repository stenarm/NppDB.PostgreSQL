using System.Windows.Forms;
using Npgsql;

namespace NppDB.PostgreSQL
{
    internal class PostgreSQLViewGroup : PostgreSQLTableGroup
    {
        public PostgreSQLViewGroup()
        {
            Query = "SELECT table_name " +
                "FROM information_schema.tables tbls " +
                "WHERE table_schema='{0}' " +
                "AND table_type='VIEW' " +
                "UNION " +
                "SELECT c.relname AS table_name " +
                "FROM pg_namespace AS n " +
                "INNER JOIN pg_class AS c " +
                "ON n.oid=c.relnamespace " +
                "AND relkind='m' " +
                "AND n.nspname = '{0}' " +
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
