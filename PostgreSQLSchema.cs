using NppDB.Comm;
using System.Windows.Forms;

namespace NppDB.PostgreSQL
{
    public class PostgreSqlSchema : TreeNode, IRefreshable, IMenuProvider
    {
        public string Schema { get; set; }
        public bool Foreign { get; set; }
        public PostgreSqlSchema()
        {
            SelectedImageKey = ImageKey = "Database";
            Refresh();
        }

        public void Refresh()
        {
            Nodes.Clear();

            var tableGroup = new PostgreSqlTableGroup();
            tableGroup.Nodes.Add(new TreeNode("")); // Add dummy node
            Nodes.Add(tableGroup);

            var foreignTableGroup = new PostgreSQLForeignTableGroup();
            foreignTableGroup.Nodes.Add(new TreeNode("")); // Add dummy node
            Nodes.Add(foreignTableGroup);

            var viewGroup = new PostgreSQLViewGroup();
            viewGroup.Nodes.Add(new TreeNode("")); // Add dummy node
            Nodes.Add(viewGroup);

            var matViewGroup = new PostgreSQLMaterializedViewGroup();
            matViewGroup.Nodes.Add(new TreeNode("")); // Add dummy node
            Nodes.Add(matViewGroup);

            var functionGroup = new PostgreSQLFunctionGroup();
            functionGroup.Nodes.Add(new TreeNode("")); // Add dummy node
            Nodes.Add(functionGroup);

            // Commented out MS Access ones remain commented
            //Nodes.Add(new MSAccessTableGroup());
            //Nodes.Add(new MSAccessViewGroup());
        }

        public ContextMenuStrip GetMenu()
        {
            var menuList = new ContextMenuStrip { ShowImageMargin = false };
            menuList.Items.Add(new ToolStripButton("Refresh", null, (s, e) => { Refresh(); }));
            var connect = GetDBConnect();
            if (connect?.CommandHost == null) return menuList;

            //menuList.Items.Add(new ToolStripSeparator());
            //var host = connect.CommandHost;
            //menuList.Items.Add(new ToolStripButton("Set as default path", null, (s, e) =>
            //{
            //    var id = host.Execute(NppDBCommandType.GetActivatedBufferID, null);
            //    var query = $"ALTER ROLE {connect.Account} SET search_path TO {Schema};";
            //    host.Execute(NppDBCommandType.ExecuteSQL, new[] { id, query });
            //    connect.Refresh();
            //}));
            return menuList;
        }

        private PostgreSqlConnect GetDBConnect()
        {
            var connect = Parent as PostgreSqlConnect;
            return connect;
        }
    }
}
