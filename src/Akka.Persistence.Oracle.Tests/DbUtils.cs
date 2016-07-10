using System.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Tests
{
    public static class DbUtils
    {
        public static void Clean()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["TestDb"].ConnectionString;
            var connectionBuilder = new OracleConnectionStringBuilder(connectionString);

            var schemaName = connectionBuilder.UserID;
            using (var conn = new OracleConnection(connectionString))
            {
                conn.Open();
                DropTables(conn, schemaName);
            }
        }

        private static void DropTables(OracleConnection conn, string schemaName)
        {
            using (var cmd = new OracleCommand())
            {
                cmd.CommandText = string.Format(@"
BEGIN
  BEGIN EXECUTE IMMEDIATE 'DROP TABLE {0}.EVENTJOURNAL'; EXCEPTION WHEN OTHERS THEN NULL; END;
  BEGIN EXECUTE IMMEDIATE 'DROP TABLE {0}.SNAPSHOTSTORE'; EXCEPTION WHEN OTHERS THEN NULL; END;
  BEGIN EXECUTE IMMEDIATE 'DROP TABLE {0}.METADATA'; EXCEPTION WHEN OTHERS THEN NULL; END;
END;", schemaName);

                cmd.Connection = conn;
                cmd.ExecuteNonQuery();
            }
        }
    }
}
