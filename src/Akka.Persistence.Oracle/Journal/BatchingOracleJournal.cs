//-----------------------------------------------------------------------
// <copyright file="OracleJournal.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql.Common.Journal;
using Akka.Serialization;
using Akka.Util;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Journal
{
    public class BatchingOracleJournal : BatchingSqlJournal<OracleConnection, OracleCommand>
    {
        private readonly Akka.Serialization.Serialization serialization;

        protected override string AllPersistenceIdsSql { get; }
        protected override string HighestSequenceNrSql { get; }
        protected override string DeleteBatchSql { get; }
        protected override string UpdateSequenceNrSql { get; }
        protected override string ByPersistenceIdSql { get; }
        protected override string ByTagSql { get; }
        protected override string InsertEventSql { get; }

        public BatchingOracleJournal(Config config)
            : this(new BatchingOracleJournalSetup(config))
        { }

        public BatchingOracleJournal(BatchingSqlJournalSetup setup)
            : base(setup)
        {
            serialization = Context.System.Serialization;
            var conventions = Setup.NamingConventions;

            var allEventColumnNames = $@"
                e.{conventions.PersistenceIdColumnName} AS PersistenceId, 
                e.{conventions.SequenceNrColumnName} AS SequenceNr, 
                e.{conventions.TimestampColumnName} AS Timestamp, 
                e.{conventions.IsDeletedColumnName} AS IsDeleted, 
                e.{conventions.ManifestColumnName} AS Manifest, 
                e.{conventions.PayloadColumnName} AS Payload, 
                e.{conventions.SerializerIdColumnName} AS SerializerId";

            AllPersistenceIdsSql = $@"
SELECT DISTINCT e.{conventions.PersistenceIdColumnName} AS PersistenceId 
FROM {conventions.FullJournalTableName} e";

            HighestSequenceNrSql = $@"
SELECT MAX(u.SeqNr) AS SequenceNr 
FROM (
    SELECT e.{conventions.SequenceNrColumnName} AS SeqNr FROM {conventions.FullJournalTableName} e WHERE e.{conventions.PersistenceIdColumnName} = :PersistenceId 
    UNION 
    SELECT m.{conventions.SequenceNrColumnName} AS SeqNr FROM {conventions.FullMetaTableName} m WHERE m.{conventions.PersistenceIdColumnName} = :PersistenceId
) u
ORDER BY SequenceNr DESC";

            DeleteBatchSql = $@"
DELETE FROM {conventions.FullJournalTableName} 
WHERE {conventions.PersistenceIdColumnName} = :PersistenceId AND {conventions.SequenceNrColumnName} <= :ToSequenceNr";

            UpdateSequenceNrSql = $@"
MERGE INTO {conventions.FullMetaTableName} USING DUAL ON ({conventions.PersistenceIdColumnName} = :PersistenceId)
WHEN MATCHED THEN UPDATE SET {conventions.SequenceNrColumnName} = :SequenceNr
WHEN NOT MATCHED THEN INSERT ({conventions.PersistenceIdColumnName}, {conventions.SequenceNrColumnName}) VALUES (:PersistenceId, :SequenceNr)";

            ByPersistenceIdSql = $@"
SELECT {allEventColumnNames}
FROM {conventions.FullJournalTableName} e
WHERE e.{conventions.PersistenceIdColumnName} = :PersistenceId AND e.{conventions.SequenceNrColumnName} BETWEEN :FromSequenceNr AND :ToSequenceNr
ORDER BY e.{conventions.SequenceNrColumnName} ASC";

            ByTagSql = $@"
SELECT {conventions.PersistenceIdColumnName} AS PersistenceId,
    {conventions.SequenceNrColumnName} AS SequenceNr, 
    {conventions.TimestampColumnName} AS Timestamp, 
    {conventions.IsDeletedColumnName} AS IsDeleted, 
    {conventions.ManifestColumnName} AS Manifest, 
    {conventions.PayloadColumnName} AS Payload, 
    {conventions.SerializerIdColumnName} AS SerializerId,
    {conventions.OrderingColumnName} AS Ordering
FROM (
    SELECT {allEventColumnNames}, e.{conventions.OrderingColumnName} AS Ordering, ROW_NUMBER() OVER (ORDER BY e.{conventions.OrderingColumnName} ASC) AS RN
    FROM {conventions.FullJournalTableName} e
    WHERE e.{conventions.OrderingColumnName} > :Ordering AND e.{conventions.TagsColumnName} LIKE :Tag
)
WHERE RN <= :Take";

            InsertEventSql = $@"
INSERT INTO {conventions.FullJournalTableName} (
    {conventions.PersistenceIdColumnName},
    {conventions.SequenceNrColumnName},
    {conventions.TimestampColumnName},
    {conventions.IsDeletedColumnName},
    {conventions.ManifestColumnName},
    {conventions.PayloadColumnName},
    {conventions.TagsColumnName},
    {conventions.SerializerIdColumnName}
) VALUES (:PersistenceId, :SequenceNr, :Timestamp, :IsDeleted, :Manifest, :Payload, :Tag, :SerializerId)";

            Initializers = ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, string>("CreateJournalSql", $@"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{conventions.JournalEventsTableName}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE '
            CREATE TABLE {conventions.FullJournalTableName} (
                {conventions.OrderingColumnName} NUMBER(19,0) NOT NULL,
                {conventions.PersistenceIdColumnName} NVARCHAR2(255) NOT NULL,
                {conventions.SequenceNrColumnName} NUMBER(19,0) NOT NULL,
                {conventions.TimestampColumnName} NUMBER(19,0) NOT NULL,
                {conventions.IsDeletedColumnName} NUMBER(1,0) DEFAULT(0) NOT NULL CHECK (IsDeleted IN (0,1)),
                {conventions.ManifestColumnName} NVARCHAR2(500) NOT NULL,
                {conventions.PayloadColumnName} BLOB NOT NULL,
                {conventions.TagsColumnName} NVARCHAR2(100) NULL,
                {conventions.SerializerIdColumnName} NUMBER(10,0) NULL,
                CONSTRAINT PK_{conventions.JournalEventsTableName} PRIMARY KEY ({conventions.OrderingColumnName}),
                CONSTRAINT QU_{conventions.JournalEventsTableName} UNIQUE({conventions.PersistenceIdColumnName}, {conventions.SequenceNrColumnName})
            )';

        EXECUTE IMMEDIATE '
            CREATE SEQUENCE {conventions.FullJournalTableName}_SEQ
                START WITH 1
                INCREMENT BY 1
                CACHE 1000
                ORDER
                NOCYCLE
                NOMAXVALUE
            ';

        EXECUTE IMMEDIATE '
            CREATE OR REPLACE TRIGGER {conventions.FullJournalTableName}_TRG 
             BEFORE INSERT ON {conventions.JournalEventsTableName} 
             FOR EACH ROW
             BEGIN
                :new.{conventions.OrderingColumnName} := {conventions.JournalEventsTableName}_SEQ.NEXTVAL;
             END;
            ';

        EXECUTE IMMEDIATE 'ALTER TRIGGER {conventions.FullJournalTableName}_TRG ENABLE';

        EXECUTE IMMEDIATE 'CREATE INDEX IX_{conventions.JournalEventsTableName}_{conventions.SequenceNrColumnName} ON {conventions.FullJournalTableName}({conventions.SequenceNrColumnName})';
        EXECUTE IMMEDIATE 'CREATE INDEX IX_{conventions.JournalEventsTableName}_{conventions.TimestampColumnName} ON {conventions.FullJournalTableName}({conventions.TimestampColumnName})';       
    END IF;
END;"),
                new KeyValuePair<string, string>("CreateMetadataSql", $@"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{conventions.MetaTableName}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE(
            'CREATE TABLE {conventions.FullMetaTableName} (
                {conventions.PersistenceIdColumnName} NVARCHAR2(255) NOT NULL,
                {conventions.SequenceNrColumnName} NUMBER(19,0) NOT NULL,
                CONSTRAINT PK_{conventions.MetaTableName} PRIMARY KEY ({conventions.PersistenceIdColumnName}, {conventions.SequenceNrColumnName})
            )'
        );
    END IF;
END;")
            });
        }

        protected override ImmutableDictionary<string, string> Initializers { get; }
        protected override OracleConnection CreateConnection(string connectionString) => new OracleConnection(connectionString);

        private static void AddParameter(OracleCommand command, string parameterName, OracleDbType parameterType, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.OracleDbType = parameterType;
            parameter.Value = value;

            command.Parameters.Add(parameter);
        }

        protected override IPersistentRepresentation ReadEvent(DbDataReader reader)
        {
            var persistenceId = reader.GetString(PersistenceIdIndex);
            var sequenceNr = reader.GetInt64(SequenceNrIndex);
            var isDeleted = Convert.ToBoolean(reader.GetInt16(IsDeletedIndex));
            var manifest = reader.GetString(ManifestIndex).Trim(); // HACK
            var payload = reader[PayloadIndex];

            object deserialized;
            if (reader.IsDBNull(SerializerIdIndex))
            {
                var type = Type.GetType(manifest, true);
                var deserializer = serialization.FindSerializerForType(type, Setup.DefaultSerializer);
                deserialized = deserializer.FromBinary((byte[])payload, type);
            }
            else
            {
                var serializerId = reader.GetInt32(SerializerIdIndex);
                deserialized = serialization.Deserialize((byte[])payload, serializerId, manifest);
            }

            return new Persistent(deserialized, sequenceNr, persistenceId, manifest, isDeleted, ActorRefs.NoSender);
        }

        protected override void WriteEvent(OracleCommand command, IPersistentRepresentation persistent, string tags = "")
        {
            var payloadType = persistent.Payload.GetType();
            var serializer = serialization.FindSerializerForType(payloadType, Setup.DefaultSerializer);

            var manifest = " "; // HACK
            if (serializer is SerializerWithStringManifest stringManifest)
            {
                manifest = stringManifest.Manifest(persistent.Payload);
            }
            else
            {
                if (serializer.IncludeManifest)
                {
                    manifest = payloadType.TypeQualifiedName();
                }
            }

            var binary = serializer.ToBinary(persistent.Payload);

            AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistent.PersistenceId);
            AddParameter(command, ":SequenceNr", OracleDbType.Int64, persistent.SequenceNr);
            AddParameter(command, ":Timestamp", OracleDbType.Int64, DateTime.UtcNow.Ticks);
            AddParameter(command, ":IsDeleted", OracleDbType.Int16, persistent.IsDeleted);
            AddParameter(command, ":Manifest", OracleDbType.NVarchar2, manifest);
            AddParameter(command, ":Payload", OracleDbType.Blob, binary);
            AddParameter(command, ":Tag", OracleDbType.NVarchar2, tags);
            AddParameter(command, ":SerializerId", OracleDbType.Int32, serializer.Identifier);
        }

        protected override async Task<long> ReadHighestSequenceNr(string persistenceId, OracleCommand command)
        {
            command.CommandText = HighestSequenceNrSql;

            command.Parameters.Clear();
            AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);

            var result = await command.ExecuteScalarAsync();
            return result is decimal ? Convert.ToInt64(result) : 0L;
        }

        protected override async Task HandleDeleteMessagesTo(DeleteMessagesTo req, OracleCommand command)
        {
            var toSequenceNr = req.ToSequenceNr;
            var persistenceId = req.PersistenceId;

            NotifyNewPersistenceIdAdded(persistenceId);

            try
            {
                var highestSequenceNr = await ReadHighestSequenceNr(persistenceId, command);

                command.CommandText = DeleteBatchSql;
                command.Parameters.Clear();
                AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
                AddParameter(command, ":ToSequenceNr", OracleDbType.Int64, toSequenceNr);

                await command.ExecuteNonQueryAsync();

                if (highestSequenceNr <= toSequenceNr)
                {
                    command.CommandText = UpdateSequenceNrSql;
                    command.Parameters.Clear();

                    AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
                    AddParameter(command, ":SequenceNr", OracleDbType.Int64, highestSequenceNr);

                    await command.ExecuteNonQueryAsync();
                }

                var response = new DeleteMessagesSuccess(toSequenceNr);
                req.PersistentActor.Tell(response);
            }
            catch (Exception cause)
            {
                var response = new DeleteMessagesFailure(cause, toSequenceNr);
                req.PersistentActor.Tell(response, ActorRefs.NoSender);
            }
        }

        protected override async Task HandleReplayTaggedMessages(ReplayTaggedMessages req, OracleCommand command)
        {
            var replyTo = req.ReplyTo;

            try
            {
                var maxSequenceNr = 0L;
                var tag = req.Tag;
                var toOffset = req.ToOffset;
                var fromOffset = req.FromOffset;
                var take = Math.Min(toOffset - fromOffset, req.Max);

                command.CommandText = ByTagSql;
                command.Parameters.Clear();

                AddParameter(command, ":Ordering", OracleDbType.Int64, fromOffset);
                AddParameter(command, ":Tag", OracleDbType.NVarchar2, "%;" + tag + ";%");
                AddParameter(command, ":Take", OracleDbType.Int64, take);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var persistent = ReadEvent(reader);
                        var ordering = reader.GetInt64(OrderingIndex);
                        maxSequenceNr = Math.Max(maxSequenceNr, persistent.SequenceNr);

                        foreach (var adapted in AdaptFromJournal(persistent))
                        {
                            replyTo.Tell(new ReplayedTaggedMessage(adapted, tag, ordering), ActorRefs.NoSender);
                        }
                    }
                }

                replyTo.Tell(new RecoverySuccess(maxSequenceNr));
            }
            catch (Exception cause)
            {
                replyTo.Tell(new ReplayMessagesFailure(cause));
            }
        }

        protected override async Task HandleReplayMessages(ReplayMessages req, OracleCommand command, IActorContext context)
        {
            var replaySettings = Setup.ReplayFilterSettings;
            var replyTo = replaySettings.IsEnabled
                ? context.ActorOf(ReplayFilter.Props(req.PersistentActor, replaySettings.Mode, replaySettings.WindowSize, replaySettings.MaxOldWriters, replaySettings.IsDebug))
                : req.PersistentActor;
            var persistenceId = req.PersistenceId;

            NotifyNewPersistenceIdAdded(persistenceId);

            try
            {
                var highestSequenceNr = await ReadHighestSequenceNr(persistenceId, command);
                var toSequenceNr = Math.Min(req.ToSequenceNr, highestSequenceNr);

                command.CommandText = ByPersistenceIdSql;
                command.Parameters.Clear();

                AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
                AddParameter(command, ":FromSequenceNr", OracleDbType.Int64, req.FromSequenceNr);
                AddParameter(command, ":ToSequenceNr", OracleDbType.Int64, toSequenceNr);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var i = 0L;
                    while (i++ < req.Max && await reader.ReadAsync())
                    {
                        var persistent = ReadEvent(reader);
                        if (persistent.IsDeleted)
                        {
                            continue;
                        }

                        foreach (var adaptedRepresentation in AdaptFromJournal(persistent))
                        {
                            replyTo.Tell(new ReplayedMessage(adaptedRepresentation), ActorRefs.NoSender);
                        }
                    }
                }

                var response = new RecoverySuccess(highestSequenceNr);
                replyTo.Tell(response, ActorRefs.NoSender);
            }
            catch (Exception cause)
            {
                var response = new ReplayMessagesFailure(cause);
                replyTo.Tell(response, ActorRefs.NoSender);
            }
        }
    }
}
