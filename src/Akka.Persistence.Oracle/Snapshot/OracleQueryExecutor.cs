//-----------------------------------------------------------------------
// <copyright file="OracleQueryExecutor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Common.Snapshot;
using Akka.Serialization;
using Akka.Util;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Snapshot
{
    public class OracleQueryExecutor : AbstractQueryExecutor
    {
        protected override string SelectSnapshotSql { get; }
        protected override string DeleteSnapshotSql { get; }
        protected override string DeleteSnapshotRangeSql { get; }
        protected override string InsertSnapshotSql { get; }
        protected override string CreateSnapshotTableSql { get; }

        public OracleQueryExecutor(QueryConfiguration configuration, Akka.Serialization.Serialization serialization)
            : base(configuration, serialization)
        {
            SelectSnapshotSql = $@"
SELECT * 
FROM (
    SELECT {Configuration.PersistenceIdColumnName},
        {Configuration.SequenceNrColumnName}, 
        {Configuration.TimestampColumnName}, 
        {Configuration.ManifestColumnName}, 
        {Configuration.PayloadColumnName},
        {Configuration.SerializerIdColumnName}
    FROM {Configuration.FullSnapshotTableName} 
    WHERE {Configuration.PersistenceIdColumnName} = :PersistenceId AND {Configuration.SequenceNrColumnName} <= :SequenceNr AND {Configuration.TimestampColumnName} <= :Timestamp
    ORDER BY {Configuration.SequenceNrColumnName} DESC
)
WHERE ROWNUM = 1";

            DeleteSnapshotSql = $@"
DELETE FROM {Configuration.FullSnapshotTableName}
WHERE {Configuration.PersistenceIdColumnName} = :PersistenceId AND {Configuration.SequenceNrColumnName} = :SequenceNr";

            DeleteSnapshotRangeSql = $@"
DELETE FROM {Configuration.FullSnapshotTableName}
WHERE {Configuration.PersistenceIdColumnName} = :PersistenceId AND {Configuration.SequenceNrColumnName} <= :SequenceNr AND {Configuration.TimestampColumnName} <= :Timestamp";

            InsertSnapshotSql = $@"
DECLARE
    value_count integer;
BEGIN
    SELECT COUNT(1) INTO value_count 
    FROM {configuration.FullSnapshotTableName} 
    WHERE {configuration.PersistenceIdColumnName} = :PersistenceId AND {configuration.SequenceNrColumnName} = :SequenceNr;

    IF (value_count > 0) THEN
        UPDATE {configuration.FullSnapshotTableName} 
        SET {configuration.TimestampColumnName} = :Timestamp, {configuration.ManifestColumnName} = :Manifest, {configuration.PayloadColumnName} = :Payload, {configuration.SerializerIdColumnName} = :SerializerId
        WHERE {configuration.PersistenceIdColumnName} = :PersistenceId AND {configuration.SequenceNrColumnName} = :SequenceNr;
    ELSE 
        INSERT INTO {Configuration.FullSnapshotTableName} ({configuration.PersistenceIdColumnName}, {configuration.SequenceNrColumnName}, {configuration.TimestampColumnName}, {configuration.ManifestColumnName}, {configuration.PayloadColumnName}, {configuration.SerializerIdColumnName}) 
        VALUES (:PersistenceId, :SequenceNr, :Timestamp, :Manifest, :Payload, :SerializerId);    
    END IF;
END;";

            CreateSnapshotTableSql = $@"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{configuration.SnapshotTableName}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE(
            'CREATE TABLE {configuration.FullSnapshotTableName} (
              {configuration.PersistenceIdColumnName} NVARCHAR2(255) NOT NULL,
              {configuration.SequenceNrColumnName} NUMBER(19,0) NOT NULL,
              {configuration.TimestampColumnName} TIMESTAMP(7) NOT NULL,
              {configuration.ManifestColumnName} NVARCHAR2(500) NOT NULL,
              {configuration.PayloadColumnName} BLOB NOT NULL,
              {configuration.SerializerIdColumnName} NUMBER(10,0) NULL,
              CONSTRAINT PK_{configuration.SnapshotTableName} PRIMARY KEY ({configuration.PersistenceIdColumnName}, {configuration.SequenceNrColumnName})
            )'
        );
        EXECUTE IMMEDIATE ('CREATE INDEX IX_{configuration.SnapshotTableName}_{configuration.SequenceNrColumnName} ON {configuration.FullSnapshotTableName}({configuration.SequenceNrColumnName})');
        EXECUTE IMMEDIATE ('CREATE INDEX IX_{configuration.SnapshotTableName}_{configuration.TimestampColumnName} ON {configuration.FullSnapshotTableName}({configuration.TimestampColumnName})');         
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

        protected override void SetTimestampParameter(DateTime timestamp, DbCommand command) => AddParameter(command, ":Timestamp", OracleDbType.TimeStamp, timestamp);
        protected override void SetSequenceNrParameter(long sequenceNr, DbCommand command) => AddParameter(command, ":SequenceNr", OracleDbType.Int64, sequenceNr);
        protected override void SetPersistenceIdParameter(string persistenceId, DbCommand command) => AddParameter(command, ":PersistenceId", OracleDbType.NVarchar2, persistenceId);
        protected override void SetPayloadParameter(object snapshot, DbCommand command)
        {
            var snapshotType = snapshot.GetType();
            var serializer = Serialization.FindSerializerForType(snapshotType, Configuration.DefaultSerializer);
            var binary = serializer.ToBinary(snapshot);
            AddParameter(command, ":Payload", OracleDbType.Blob, binary);
        }

        protected override void SetManifestParameters(object snapshot, DbCommand command)
        {
            var snapshotType = snapshot.GetType();
            var serializer = Serialization.FindSerializerForType(snapshotType, Configuration.DefaultSerializer);

            var manifest = " "; // HACK
            if (serializer is SerializerWithStringManifest stringManifest)
            {
                manifest = stringManifest.Manifest(snapshot);
            }
            else
            {
                if (serializer.IncludeManifest)
                {
                    manifest = snapshotType.TypeQualifiedName();
                }
            }
            AddParameter(command, ":Manifest", DbType.String, manifest);
            AddParameter(command, ":SerializerId", DbType.Int32, serializer.Identifier);
        }

        public override async Task DeleteAsync(DbConnection connection, CancellationToken cancellationToken, string persistenceId, long sequenceNr, DateTime? timestamp)
        {
            var sql = timestamp.HasValue
                    ? DeleteSnapshotSql + " AND {Configuration.TimestampColumnName} = :Timestamp"
                    : DeleteSnapshotSql;

            using (var command = GetCommand(connection, sql))
            using (var tx = connection.BeginTransaction())
            {
                command.Transaction = tx;

                SetPersistenceIdParameter(persistenceId, command);
                SetSequenceNrParameter(sequenceNr, command);

                if (timestamp.HasValue)
                {
                    SetTimestampParameter(timestamp.Value, command);
                }

                await command.ExecuteNonQueryAsync(cancellationToken);

                tx.Commit();
            }
        }

        protected override SelectedSnapshot ReadSnapshot(DbDataReader reader)
        {
            var persistenceId = reader.GetString(0);
            var sequenceNr = reader.GetInt64(1);
            var timestamp = reader.GetDateTime(2);

            var metadata = new SnapshotMetadata(persistenceId, sequenceNr, timestamp);
            var snapshot = GetSnapshot(reader);

            return new SelectedSnapshot(metadata, snapshot);
        }

        protected new object GetSnapshot(DbDataReader reader)
        {
            var manifest = reader.GetString(3).Trim(); // HACK
            var binary = (byte[])reader[4];

            object obj;
            if (reader.IsDBNull(5))
            {
                var type = Type.GetType(manifest, true);
                var serializer = Serialization.FindSerializerForType(type, Configuration.DefaultSerializer);
                obj = serializer.FromBinary(binary, type);
            }
            else
            {
                var serializerId = reader.GetInt32(5);
                obj = Serialization.Deserialize(binary, serializerId, manifest);
            }

            return obj;
        }
    }
}
