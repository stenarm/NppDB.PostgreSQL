using NppDB.Comm;
using System;
using System.Windows.Forms;
using Npgsql;

namespace NppDB.PostgreSQL
{
    public class PostgreSqlTableGroup : TreeNode, IRefreshable, IMenuProvider
    {
        public PostgreSqlTableGroup()
        {
            Query = "SELECT table_name FROM information_schema.tables WHERE table_schema='{0}' AND table_type in ('BASE TABLE') ORDER BY table_name";
            Text = "Base Tables";
            SelectedImageKey = ImageKey = "Group";
        }

        protected string Query { get; set; }

        protected virtual TreeNode CreateTreeNode(NpgsqlDataReader reader)
        {
            var tableNode = new PostgreSqlTable
            {
                Text = reader["table_name"].ToString()
            };

            tableNode.Nodes.Add(new TreeNode(""));

            return tableNode;
        }

        public void Refresh()
        {
            var conn = (PostgreSqlConnect)Parent.Parent;
            using (var cnn = conn.GetConnection())
            {
                TreeView.Cursor = Cursors.WaitCursor;
                TreeView.Enabled = false;
                try
                {
                    cnn.Open();
                    Nodes.Clear();
                    using (var command = new NpgsqlCommand(string.Format(Query, Parent.Text), cnn))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Nodes.Add(CreateTreeNode(reader));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, @"Exception");
                }
                finally
                {
                    cnn.Close();
                    TreeView.Enabled = true;
                    TreeView.Cursor = null;
                }
            }
        }

        public ContextMenuStrip GetMenu()
        {
            var menuList = new ContextMenuStrip { ShowImageMargin = false };
            menuList.Items.Add(new ToolStripButton("Refresh", null, (s, e) => { Refresh(); }));

            return menuList;
        }
    }
}
