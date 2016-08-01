using System.Data.Common;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Common.Journal;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Journal
{
    /// <summary>
    /// Persistent journal actor using Oracle as persistence layer. It processes write requests
    /// one by one in asynchronous manner, while reading results asynchronously.
    /// </summary>
    public class OracleJournal : SqlJournal
    {
        private readonly string updateSequenceNrSql;
        public readonly OraclePersistence Extension = OraclePersistence.Get(Context.System);

        public OracleJournal()
            : base(new OracleJournalEngine(Context.System))
        {
            var schemaName = Extension.JournalSettings.SchemaName;
            var tableName = Extension.JournalSettings.MetadataTableName;

            updateSequenceNrSql = @"
MERGE INTO {0}.{1} USING DUAL ON (PersistenceId = :PersistenceId)
WHEN MATCHED THEN UPDATE SET SequenceNr = :SequenceNr 
WHEN NOT MATCHED THEN INSERT (PersistenceId, SequenceNr) VALUES (:PersistenceId, :SequenceNr)".QuoteSchemaAndTable(schemaName, tableName);
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            var highestSequenceNr = await DbEngine.ReadHighestSequenceNrAsync(persistenceId, 0);
            await base.DeleteMessagesToAsync(persistenceId, toSequenceNr);

            if (highestSequenceNr <= toSequenceNr)
            {
                await UpdateSequenceNr(persistenceId, highestSequenceNr);
            }
        }

        private async Task UpdateSequenceNr(string persistenceId, long toSequenceNr)
        {
            using (var connection = DbEngine.CreateDbConnection())
            {
                await connection.OpenAsync();

                using (DbCommand sqlCommand = new OracleCommand(updateSequenceNrSql) { BindByName = true })
                {
                    sqlCommand.Parameters.Add(new OracleParameter(":PersistenceId", OracleDbType.NVarchar2, persistenceId.Length) { Value = persistenceId });
                    sqlCommand.Parameters.Add(new OracleParameter(":SequenceNr", OracleDbType.Int64) { Value = toSequenceNr });

                    sqlCommand.Connection = connection;
                    sqlCommand.CommandTimeout = (int)Extension.JournalSettings.ConnectionTimeout.TotalMilliseconds;

                    await sqlCommand.ExecuteNonQueryAsync();
                }
            }
        }
    }
}