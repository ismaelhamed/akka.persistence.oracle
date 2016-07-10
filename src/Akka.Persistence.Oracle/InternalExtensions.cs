using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle
{
    internal static class InternalExtensions
    {
        public static string QuoteSchemaAndTable(this string sqlQuery, string schemaName, string tableName)
        {
            var builder = new OracleCommandBuilder();
            return string.Format(sqlQuery, builder.QuoteIdentifier(schemaName), builder.QuoteIdentifier(tableName));
        }
    }
}