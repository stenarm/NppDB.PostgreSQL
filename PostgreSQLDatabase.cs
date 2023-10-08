using NppDB.Comm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NppDB.PostgreSQL
{
    public class PostgreSQLDatabase : TreeNode, IRefreshable, IMenuProvider
    {
        public PostgreSQLDatabase()
        {
            SelectedImageKey = ImageKey = "Database";
            Refresh();
        }

        public void Refresh()
        {
            Nodes.Clear();
            Nodes.Add(new PostgreSQLTableGroup());
            Nodes.Add(new PostgreSQLViewGroup());
            //Nodes.Add(new MSAccessTableGroup());
            //Nodes.Add(new MSAccessViewGroup());
            // add other categories as stored procedures
        }

        public ContextMenuStrip GetMenu()
        {
            var menuList = new ContextMenuStrip { ShowImageMargin = false };
            menuList.Items.Add(new ToolStripButton("Refresh", null, (s, e) => { Refresh(); }));
            return menuList;
        }
    }
}
