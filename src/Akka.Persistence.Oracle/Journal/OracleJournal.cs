//-----------------------------------------------------------------------
// <copyright file="OracleJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Data.Common;
using Akka.Configuration;
using Akka.Persistence.Sql.Common.Journal;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Journal
{
    public class OracleJournal : SqlJournal
    {
        public static readonly OraclePersistence Extension = OraclePersistence.Get(Context.System);

        public OracleJournal(Config journalConfig) 
            : base(journalConfig)
        {
            var config = journalConfig.WithFallback(Extension.DefaultJournalConfig);
            QueryExecutor = new OracleQueryExecutor(new QueryConfiguration(
                schemaName: config.GetString("schema-name"),
                journalEventsTableName: config.GetString("table-name"),
                metaTableName: config.GetString("metadata-table-name"),
                persistenceIdColumnName: "PersistenceId",
                sequenceNrColumnName: "SequenceNr",
                payloadColumnName: "Payload",
                manifestColumnName: "Manifest",
                timestampColumnName: "Timestamp",
                isDeletedColumnName: "IsDeleted",
                tagsColumnName: "Tags",
                orderingColumnName: "Ordering",
                serializerIdColumnName: "SerializerId",
                timeout: config.GetTimeSpan("connection-timeout"),
                defaultSerializer: config.GetString("serializer"),
                useSequentialAccess: config.GetBoolean("use-sequential-access")),
                Context.System.Serialization, 
                GetTimestampProvider(config.GetString("timestamp-provider")));
        }

        protected override DbConnection CreateDbConnection(string connectionString) => new OracleConnection(connectionString);

        protected override string JournalConfigPath => OracleJournalSettings.ConfigPath;
        public override IJournalQueryExecutor QueryExecutor { get; }
    }
}