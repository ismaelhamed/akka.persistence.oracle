//-----------------------------------------------------------------------
// <copyright file="OracleQueryExecutor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Common.Journal;
using Akka.Serialization;
using Akka.Util;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Journal
{
    public class OracleQueryExecutor : AbstractQueryExecutor
    {
        protected override string AllPersistenceIdsSql { get; }
        protected override string HighestSequenceNrSql { get; }
        protected override string DeleteBatchSql { get; }
        protected override string UpdateSequenceNrSql { get; }
        protected override string ByPersistenceIdSql { get; }
        protected override string ByTagSql { get; }
        protected override string InsertEventSql { get; }
        protected override string CreateEventsJournalSql { get; }
        protected override string CreateMetaTableSql { get; }

        public OracleQueryExecutor(QueryConfiguration configuration, Akka.Serialization.Serialization serialization, ITimestampProvider timestampProvider)
            : base(configuration, serialization, timestampProvider)
        {
            var allEventColumnNames = $@"
                e.{Configuration.PersistenceIdColumnName} AS PersistenceId, 
                e.{Configuration.SequenceNrColumnName} AS SequenceNr, 
                e.{Configuration.TimestampColumnName} AS Timestamp, 
                e.{Configuration.IsDeletedColumnName} AS IsDeleted, 
                e.{Configuration.ManifestColumnName} AS Manifest, 
                e.{Configuration.PayloadColumnName} AS Payload,
                e.{Configuration.SerializerIdColumnName} AS SerializerId";

            AllPersistenceIdsSql = $@"
SELECT DISTINCT e.{Configuration.PersistenceIdColumnName} AS PersistenceId 
FROM {Configuration.FullJournalTableName} e";

            HighestSequenceNrSql = $@"
SELECT MAX(u.SeqNr) AS SequenceNr 
FROM (
    SELECT e.{Configuration.SequenceNrColumnName} AS SeqNr FROM {Configuration.FullJournalTableName} e WHERE e.{Configuration.PersistenceIdColumnName} = :PersistenceId 
    UNION 
    SELECT m.{Configuration.SequenceNrColumnName} AS SeqNr FROM {Configuration.FullMetaTableName} m WHERE m.{Configuration.PersistenceIdColumnName} = :PersistenceId
) u
ORDER BY SequenceNr DESC";

            DeleteBatchSql = $@"
DELETE FROM {Configuration.FullJournalTableName} 
WHERE {Configuration.PersistenceIdColumnName} = :PersistenceId AND {Configuration.SequenceNrColumnName} <= :ToSequenceNr";

            UpdateSequenceNrSql = $@"
MERGE INTO {Configuration.FullMetaTableName} USING DUAL ON ({Configuration.PersistenceIdColumnName} = :PersistenceId)
WHEN MATCHED THEN UPDATE SET {Configuration.SequenceNrColumnName} = :SequenceNr
WHEN NOT MATCHED THEN INSERT ({Configuration.PersistenceIdColumnName}, {Configuration.SequenceNrColumnName}) VALUES (:PersistenceId, :SequenceNr)";

            ByPersistenceIdSql = $@"
SELECT {allEventColumnNames}
FROM {Configuration.FullJournalTableName} e
WHERE e.{Configuration.PersistenceIdColumnName} = :PersistenceId AND e.{Configuration.SequenceNrColumnName} BETWEEN :FromSequenceNr AND :ToSequenceNr
ORDER BY e.{Configuration.SequenceNrColumnName} ASC";

            ByTagSql = $@"
SELECT {Configuration.PersistenceIdColumnName} AS PersistenceId,
    {Configuration.SequenceNrColumnName} AS SequenceNr, 
    {Configuration.TimestampColumnName} AS Timestamp, 
    {Configuration.IsDeletedColumnName} AS IsDeleted, 
    {Configuration.ManifestColumnName} AS Manifest, 
    {Configuration.PayloadColumnName} AS Payload, 
    {Configuration.SerializerIdColumnName} AS SerializerId,
    {Configuration.OrderingColumnName} AS Ordering
FROM (
    SELECT {allEventColumnNames}, e.{Configuration.OrderingColumnName} AS Ordering, ROW_NUMBER() OVER (ORDER BY e.{Configuration.OrderingColumnName} ASC) AS RN
    FROM {Configuration.FullJournalTableName} e
    WHERE e.{Configuration.OrderingColumnName} > :Ordering AND e.{Configuration.TagsColumnName} LIKE :Tag
)
WHERE RN <= :Take";



            InsertEventSql = $@"
INSERT INTO {Configuration.FullJournalTableName} (
    {Configuration.PersistenceIdColumnName},
    {Configuration.SequenceNrColumnName},
    {Configuration.TimestampColumnName},
    {Configuration.IsDeletedColumnName},
    {Configuration.ManifestColumnName},
    {Configuration.PayloadColumnName},
    {Configuration.TagsColumnName},
    {Configuration.SerializerIdColumnName}
) VALUES (:PersistenceId, :SequenceNr, :Timestamp, :IsDeleted, :Manifest, :Payload, :Tag, :SerializerId)";

            CreateEventsJournalSql = $@"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{configuration.JournalEventsTableName}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE '
            CREATE TABLE {configuration.FullJournalTableName} (
                {configuration.OrderingColumnName} NUMBER(19,0) NOT NULL,
                {configuration.PersistenceIdColumnName} NVARCHAR2(255) NOT NULL,
                {configuration.SequenceNrColumnName} NUMBER(19,0) NOT NULL,
                {configuration.TimestampColumnName} NUMBER(19,0) NOT NULL,
                {configuration.IsDeletedColumnName} NUMBER(1,0) DEFAULT(0) NOT NULL CHECK (IsDeleted IN (0,1)),
                {configuration.ManifestColumnName} NVARCHAR2(500) NOT NULL,
                {configuration.PayloadColumnName} BLOB NOT NULL,
                {configuration.TagsColumnName} NVARCHAR2(100) NULL,
                {configuration.SerializerIdColumnName} NUMBER(10,0) NULL,
                CONSTRAINT PK_{configuration.JournalEventsTableName} PRIMARY KEY ({configuration.OrderingColumnName}),
                CONSTRAINT QU_{configuration.JournalEventsTableName} UNIQUE({configuration.PersistenceIdColumnName}, {configuration.SequenceNrColumnName})
            )';

        EXECUTE IMMEDIATE '
            CREATE SEQUENCE {configuration.FullJournalTableName}_SEQ
                START WITH 1
                INCREMENT BY 1
                CACHE 1000
                ORDER
                NOCYCLE
                NOMAXVALUE
            ';

        EXECUTE IMMEDIATE '
            CREATE OR REPLACE TRIGGER {configuration.FullJournalTableName}_TRG 
             BEFORE INSERT ON {configuration.JournalEventsTableName} 
             FOR EACH ROW
             BEGIN
                :new.{configuration.OrderingColumnName} := {configuration.JournalEventsTableName}_SEQ.NEXTVAL;
             END;
            ';

        EXECUTE IMMEDIATE 'ALTER TRIGGER {configuration.FullJournalTableName}_TRG ENABLE';

        EXECUTE IMMEDIATE 'CREATE INDEX IX_{configuration.JournalEventsTableName}_{configuration.SequenceNrColumnName} ON {configuration.FullJournalTableName}({configuration.SequenceNrColumnName})';
        EXECUTE IMMEDIATE 'CREATE INDEX IX_{configuration.JournalEventsTableName}_{configuration.TimestampColumnName} ON {configuration.FullJournalTableName}({configuration.TimestampColumnName})';       
    END IF;
END;";

            CreateMetaTableSql = $@"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{configuration.MetaTableName}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE(
            'CREATE TABLE {configuration.FullMetaTableName} (
                {configuration.PersistenceIdColumnName} NVARCHAR2(255) NOT NULL,
                {configuration.SequenceNrColumnName} NUMBER(19,0) NOT NULL,
                CONSTRAINT PK_{configuration.MetaTableName} PRIMARY KEY ({configuration.PersistenceIdColumnName}, {configuration.SequenceNrColumnName})
            )'
        );        
    END IF;
END;";
        }

        protected override DbCommand CreateCommand(DbConnection connection) => new OracleCommand { Connection = (OracleConnection)connection, BindByName = true };

        private static void AddParameter(DbCommand command, string parameterName, OracleDbType parameterType, object value)
        {
            var parameter = ((OracleCommand)command).CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = parameterType;
            parameter.Value = value;

            command.Parameters.Add(parameter);
        }

        protected override IPersistentRepresentation ReadEvent(DbDataReader reader)
        {
            var persistenceId = reader.GetString(PersistenceIdIndex);
            var sequenceNr = reader.GetInt64(SequenceNrIndex);
            var timestamp = reader.GetInt64(TimestampIndex);
            var isDeleted = Convert.ToBoolean(reader.GetInt16(IsDeletedIndex));
            var manifest = reader.GetString(ManifestIndex).Trim(); // HACK
            var payload = reader[PayloadIndex];

            object deserialized;
            if (reader.IsDBNull(SerializerIdIndex))
            {
                var type = Type.GetType(manifest, true);
                var deserializer = Serialization.FindSerializerForType(type, Configuration.DefaultSerializer);
                deserialized = deserializer.FromBinary((byte[])payload, type);
            }
            else
            {
                var serializerId = reader.GetInt32(SerializerIdIndex);
                deserialized = Serialization.Deserialize((byte[])payload, serializerId, manifest);
            }

            return new Persistent(deserialized, sequenceNr, persistenceId, manifest, isDeleted, ActorRefs.NoSender);
        }

        protected override void WriteEvent(DbCommand command, IPersistentRepresentation e, IImmutableSet<string> tags)
        {
            var payloadType = e.Payload.GetType();
            var serializer = Serialization.FindSerializerForType(payloadType, Configuration.DefaultSerializer);

            var manifest = " "; // HACK
            if (serializer is SerializerWithStringManifest stringManifest)
            {
                manifest = stringManifest.Manifest(e.Payload);
            }
            else
            {
                if (serializer.IncludeManifest)
                {
                    manifest = payloadType.TypeQualifiedName();
                }
            }

            var binary = serializer.ToBinary(e.Payload);

            AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, e.PersistenceId);
            AddParameter(command, ":SequenceNr", OracleDbType.Int64, e.SequenceNr);
            AddParameter(command, ":Timestamp", OracleDbType.Int64, TimestampProvider.GenerateTimestamp(e));
            AddParameter(command, ":IsDeleted", OracleDbType.Int16, e.IsDeleted);
            AddParameter(command, ":Manifest", OracleDbType.NVarchar2, manifest);
            AddParameter(command, ":Payload", OracleDbType.Blob, binary);
            AddParameter(command, ":SerializerId", OracleDbType.Int32, serializer.Identifier);

            if (tags.Count != 0)
            {
                var tagBuilder = new StringBuilder(";", tags.Sum(x => x.Length) + tags.Count + 1);
                foreach (var tag in tags)
                {
                    tagBuilder.Append(tag).Append(';');
                }
                AddParameter(command, ":Tag", OracleDbType.NVarchar2, tagBuilder.ToString());
            }
            else
            {
                AddParameter(command, ":Tag", OracleDbType.NVarchar2, DBNull.Value);
            }
        }

        public override async Task SelectByPersistenceIdAsync(DbConnection connection, CancellationToken cancellationToken, string persistenceId, long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> callback)
        {
            using (var command = (OracleCommand)GetCommand(connection, ByPersistenceIdSql))
            {
                AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
                AddParameter(command, ":FromSequenceNr", OracleDbType.Int64, fromSequenceNr);
                AddParameter(command, ":ToSequenceNr", OracleDbType.Int64, toSequenceNr);

                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    var i = 0L;
                    while (i++ < max && await reader.ReadAsync(cancellationToken))
                    {
                        var persistent = ReadEvent(reader);
                        callback(persistent);
                    }
                }
            }
        }

        public override async Task<long> SelectByTagAsync(DbConnection connection, CancellationToken cancellationToken, string tag, long fromOffset, long toOffset, long max, Action<ReplayedTaggedMessage> callback)
        {
            using (var command = (OracleCommand)GetCommand(connection, ByTagSql))
            {
                var take = Math.Min(toOffset - fromOffset, max);

                AddParameter(command, ":Ordering", OracleDbType.Int64, fromOffset);
                AddParameter(command, ":Tag", OracleDbType.NVarchar2, "%;" + tag + ";%");
                AddParameter(command, ":Take", OracleDbType.Int64, take);

                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    var maxSequenceNr = 0L;
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var persistent = ReadEvent(reader);
                        var ordering = reader.GetInt64(OrderingIndex);
                        maxSequenceNr = Math.Max(maxSequenceNr, persistent.SequenceNr);
                        callback(new ReplayedTaggedMessage(persistent, tag, ordering));
                    }

                    return maxSequenceNr;
                }
            }
        }

        public override async Task<long> SelectHighestSequenceNrAsync(DbConnection connection, CancellationToken cancellationToken, string persistenceId)
        {
            using (var command = (OracleCommand)GetCommand(connection, HighestSequenceNrSql))
            {
                AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result is decimal ? Convert.ToInt64(result) : 0L;
            }
        }

        public override async Task DeleteBatchAsync(DbConnection connection, CancellationToken cancellationToken, string persistenceId, long toSequenceNr)
        {
            using (var deleteCommand = GetCommand(connection, DeleteBatchSql))
            using (var highestSeqNrCommand = GetCommand(connection, HighestSequenceNrSql))
            {
                AddParameter(highestSeqNrCommand, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);

                AddParameter(deleteCommand, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
                AddParameter(deleteCommand, ":ToSequenceNr", OracleDbType.Int64, toSequenceNr);

                using (var tx = connection.BeginTransaction())
                {
                    deleteCommand.Transaction = tx;
                    highestSeqNrCommand.Transaction = tx;

                    var res = await highestSeqNrCommand.ExecuteScalarAsync(cancellationToken);
                    var highestSeqNr = res is decimal ? Convert.ToInt64(res) : 0L;

                    await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

                    if (highestSeqNr <= toSequenceNr)
                    {
                        using (var updateCommand = GetCommand(connection, UpdateSequenceNrSql))
                        {
                            updateCommand.Transaction = tx;

                            AddParameter(updateCommand, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
                            AddParameter(updateCommand, ":SequenceNr", OracleDbType.Int64, highestSeqNr);

                            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
                            tx.Commit();
                        }
                    }
                    else
                    {
                        tx.Commit();
                    }
                }
            }
        }
    }
}