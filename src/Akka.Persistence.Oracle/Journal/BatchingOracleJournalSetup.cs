//-----------------------------------------------------------------------
// <copyright file="OracleJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Data;
using Akka.Configuration;
using Akka.Persistence.Sql.Common.Journal;

namespace Akka.Persistence.Oracle.Journal
{
    public sealed class BatchingOracleJournalSetup : BatchingSqlJournalSetup
    {
        public BatchingOracleJournalSetup(Config config)
            : base(config, new QueryConfiguration(
                schemaName: config.GetString("schema-name", "SYSTEM"),
                journalEventsTableName: config.GetString("table-name", "EVENTJOURNAL"),
                metaTableName: config.GetString("metadata-table-name", "METADATA"),
                persistenceIdColumnName: "PersistenceId",
                sequenceNrColumnName: "SequenceNr",
                payloadColumnName: "Payload",
                manifestColumnName: "Manifest",
                timestampColumnName: "Timestamp",
                isDeletedColumnName: "IsDeleted",
                tagsColumnName: "Tags",
                orderingColumnName: "Ordering",
                serializerIdColumnName: "SerializerId",
                timeout: config.GetTimeSpan("connection-timeout", TimeSpan.FromSeconds(30)),
                defaultSerializer: config.GetString("serializer"),
                useSequentialAccess: config.GetBoolean("use-sequential-access")))
        { }

        public BatchingOracleJournalSetup(string connectionString, int maxConcurrentOperations, int maxBatchSize, int maxBufferSize, bool autoInitialize, TimeSpan connectionTimeout, IsolationLevel isolationLevel, CircuitBreakerSettings circuitBreakerSettings, ReplayFilterSettings replayFilterSettings, QueryConfiguration namingConventions, string defaultSerializer)
            : base(connectionString, maxConcurrentOperations, maxBatchSize, maxBufferSize, autoInitialize, connectionTimeout, isolationLevel, circuitBreakerSettings, replayFilterSettings, namingConventions, defaultSerializer)
        { }
    }
}
