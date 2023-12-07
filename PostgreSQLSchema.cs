using NppDB.Comm;
using System.Windows.Forms;

namespace NppDB.PostgreSQL
{
    public class PostgreSQLSchema : TreeNode, IRefreshable, IMenuProvider
    {
        public string Schema { get; set; }
        public bool Foreign { get; set; }
        public PostgreSQLSchema()
        {
            SelectedImageKey = ImageKey = "Database";
            Refresh();
        }

        public void Refresh()
        {
            Nodes.Clear();
            Nodes.Add(new PostgreSQLTableGroup());
            Nodes.Add(new PostgreSQLForeignTableGroup());
            Nodes.Add(new PostgreSQLViewGroup());
            Nodes.Add(new PostgreSQLMaterializedViewGroup());
            Nodes.Add(new PostgreSQLFunctionGroup());
            //Nodes.Add(new MSAccessTableGroup());
            //Nodes.Add(new MSAccessViewGroup());
            // add other categories as stored procedures
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

        private PostgreSQLConnect GetDBConnect()
        {
            var connect = Parent as PostgreSQLConnect;
            return connect;
        }
    }
}
