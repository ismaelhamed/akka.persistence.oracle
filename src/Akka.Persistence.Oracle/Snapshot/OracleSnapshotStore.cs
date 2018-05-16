//-----------------------------------------------------------------------
// <copyright file="OracleSnapshotStore.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Data.Common;
using Akka.Configuration;
using Akka.Persistence.Sql.Common.Snapshot;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Snapshot
{
    /// <summary>
    /// Actor used for storing incoming snapshots into persistent snapshot store backed by Oracle database.
    /// </summary>
    public class OracleSnapshotStore : SqlSnapshotStore
    {
        protected readonly OraclePersistence Extension = OraclePersistence.Get(Context.System);
        public OracleSnapshotStore(Config config) : base(config)
        {
            var sqlConfig = config.WithFallback(Extension.DefaultSnapshotConfig);
            QueryExecutor = new OracleQueryExecutor(new QueryConfiguration(
                schemaName: config.GetString("schema-name"),
                snapshotTableName: config.GetString("table-name"),
                persistenceIdColumnName: "PersistenceId",
                sequenceNrColumnName: "SequenceNr",
                payloadColumnName: "Snapshot",
                manifestColumnName: "Manifest",
                timestampColumnName: "Timestamp",
                serializerIdColumnName: "SerializerId",
                timeout: sqlConfig.GetTimeSpan("connection-timeout"), 
                defaultSerializer: config.GetString("serializer"),
                useSequentialAccess: config.GetBoolean("use-sequential-access")),
                Context.System.Serialization);
        }
        
        protected override DbConnection CreateDbConnection(string connectionString) => new OracleConnection(connectionString);
        public override ISnapshotQueryExecutor QueryExecutor { get; }
    }
}