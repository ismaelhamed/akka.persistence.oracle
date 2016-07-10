using System;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Common.Journal;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Journal
{
    /// <summary>
    /// Specialization of the <see cref="JournalDbEngine"/> which uses Oracle as it's sql backend database.
    /// </summary>
    public class OracleJournalEngine : JournalDbEngine
    {
        public readonly OracleJournalSettings OracleJournalSettings;

        public OracleJournalEngine(ActorSystem system)
            : base(system)
        {
            OracleJournalSettings = new OracleJournalSettings(system.Settings.Config.GetConfig(OracleJournalSettings.ConfigPath));

            QueryBuilder = new OracleJournalQueryBuilder(Settings.TableName, Settings.SchemaName, OracleJournalSettings.MetadataTableName);
            QueryMapper = new OracleJournalQueryMapper(system.Serialization);
        }

        protected override string JournalConfigPath
        {
            get { return OracleJournalSettings.ConfigPath; }
        }

        protected override DbConnection CreateDbConnection(string connectionString)
        {
            return new OracleConnection(connectionString);
        }

        protected override void CopyParamsToCommand(DbCommand sqlCommand, JournalEntry entry)
        {
            sqlCommand.Parameters[":PersistenceId"].Value = entry.PersistenceId;
            sqlCommand.Parameters[":SequenceNr"].Value = entry.SequenceNr;
            sqlCommand.Parameters[":Timestamp"].Value = entry.Timestamp;
            sqlCommand.Parameters[":IsDeleted"].Value = entry.IsDeleted;
            sqlCommand.Parameters[":Manifest"].Value = entry.Manifest;
            sqlCommand.Parameters[":Payload"].Value = entry.Payload;
        }

        /// <summary>
        /// Asynchronously reads a highest sequence number of the event stream related with provided <paramref name="persistenceId"/>.
        /// </summary>
        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            using (var connection = CreateDbConnection())
            {
                await connection.OpenAsync();

                var sqlCommand = QueryBuilder.SelectHighestSequenceNr(persistenceId);
                sqlCommand.Connection = connection;
                sqlCommand.CommandTimeout = (int)Settings.ConnectionTimeout.TotalMilliseconds;

                var seqNr = await sqlCommand.ExecuteScalarAsync();
                return seqNr is decimal ? Convert.ToInt64(seqNr) : 0L;
            }
        }
    }
}