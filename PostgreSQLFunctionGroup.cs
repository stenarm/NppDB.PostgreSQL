using System.Windows.Forms;
using Npgsql;

namespace NppDB.PostgreSQL
{
    internal class PostgreSQLFunctionGroup : PostgreSqlTableGroup
    {
        public PostgreSQLFunctionGroup()
        {
            Query = "select n.nspname as function_schema, " +
                "p.proname as function_name, " +
                "p.oid as function_oid, " +
                "l.lanname as function_language, " +
                "case when l.lanname = 'internal' " +
                "then p.prosrc " +
                "else pg_get_functiondef(p.oid) " +
                "end as definition, " +
                "pg_get_function_arguments(p.oid) as function_arguments, " +
                "t.typname as return_type " +
                "from pg_proc p " +
                "left join pg_namespace n on p.pronamespace = n.oid " +
                "left join pg_language l on p.prolang = l.oid " +
                "left join pg_type t on t.oid = p.prorettype " +
                "where n.nspname = '{0}' " +
                "order by function_schema, " +
                "function_name;";
            Text = "Functions";
        }

        protected override TreeNode CreateTreeNode(NpgsqlDataReader reader)
        {
            return new PostgreSQLView
            {
                Text = reader["function_name"].ToString(),
                FuncOid = reader["function_oid"].ToString(),
                TypeName = "FUNCTION"
            };
        }
    }
}
