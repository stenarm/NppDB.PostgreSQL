using System.Windows.Forms;
using Npgsql;

namespace NppDB.PostgreSQL
{
    internal class PostgreSQLMaterializedViewGroup : PostgreSQLTableGroup
    {
        public PostgreSQLMaterializedViewGroup()
        {
            Query = "SELECT c.relname AS table_name " +
                "FROM pg_namespace AS n " +
                "INNER JOIN pg_class AS c " +
                "ON n.oid=c.relnamespace " +
                "AND relkind='m' " +
                "AND n.nspname = '{0}' " +
                "ORDER BY table_name;";
            Text = "Materialized Views";
        }

        protected override TreeNode CreateTreeNode(NpgsqlDataReader reader)
        {
            return new PostgreSQLView
            {
                Text = reader["table_name"].ToString(),
                TypeName = "MATERIALIZED_VIEW"
            };
        }
    }
}
