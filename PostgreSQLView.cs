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
