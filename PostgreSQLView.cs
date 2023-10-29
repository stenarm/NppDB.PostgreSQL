namespace NppDB.PostgreSQL
{
    internal class PostgreSQLView: PostgreSQLTable
    {
        public PostgreSQLView()
        {
            TypeName = "VIEW";
            SelectedImageKey = ImageKey = "Table";
        }
    }
}
