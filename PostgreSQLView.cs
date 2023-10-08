using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NppDB.PostgreSQL
{
    internal class PostgreSQLView: PostgreSQLTable
    {
        public PostgreSQLView()
        {
            TypeName = "View";
            SelectedImageKey = ImageKey = "View";
        }
    }
}
