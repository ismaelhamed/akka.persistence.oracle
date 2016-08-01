using System;
using System.Data.Common;
using System.Text;
using Akka.Persistence.Sql.Common.Snapshot;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Snapshot
{
    internal class OracleSnapshotQueryBuilder : ISnapshotQueryBuilder
    {
        private readonly string deleteSql;
        private readonly string tableName;
        private readonly string schemaName;

        public OracleSnapshotQueryBuilder(OracleSnapshotSettings settings)
        {
            this.schemaName = settings.SchemaName;
            this.tableName = settings.TableName;

            deleteSql = @"DELETE FROM {0}.{1} WHERE PersistenceId = :PersistenceId".QuoteSchemaAndTable(schemaName, tableName);
        }

        public DbCommand DeleteOne(string persistenceId, long sequenceNr, DateTime timestamp)
        {
            var sb = new StringBuilder(deleteSql);

            var oracleCommand = new OracleCommand();
            oracleCommand.Parameters.Add(new OracleParameter(":PersistenceId", OracleDbType.NVarchar2, persistenceId.Length) { Value = persistenceId });

            if (sequenceNr < long.MaxValue && sequenceNr > 0)
            {
                sb.Append(@" AND SequenceNr = :SequenceNr");
                oracleCommand.Parameters.Add(new OracleParameter(":SequenceNr", OracleDbType.Int64) { Value = sequenceNr });
            }

            if (timestamp > DateTime.MinValue && timestamp < DateTime.MaxValue)
            {
                sb.Append(@" AND Timestamp = :Timestamp");
                oracleCommand.Parameters.Add(new OracleParameter(":Timestamp", OracleDbType.TimeStamp) { Value = timestamp });
            }

            oracleCommand.CommandText = sb.ToString();

            return oracleCommand;
        }

        public DbCommand DeleteMany(string persistenceId, long sequenceNr, DateTime maxTimestamp)
        {
            var sb = new StringBuilder(deleteSql);

            var oracleCommand = new OracleCommand();
            oracleCommand.Parameters.Add(new OracleParameter(":PersistenceId", OracleDbType.NVarchar2, persistenceId.Length) { Value = persistenceId });

            if (sequenceNr < long.MaxValue && sequenceNr > 0)
            {
                sb.Append(@" AND SequenceNr <= :SequenceNr ");
                oracleCommand.Parameters.Add(new OracleParameter(":SequenceNr", OracleDbType.Int64) { Value = sequenceNr });
            }

            if (maxTimestamp > DateTime.MinValue && maxTimestamp < DateTime.MaxValue)
            {
                sb.Append(@" AND Timestamp <= :Timestamp");
                oracleCommand.Parameters.Add(new OracleParameter(":Timestamp", OracleDbType.TimeStamp) { Value = maxTimestamp });
            }

            oracleCommand.CommandText = sb.ToString();

            return oracleCommand;
        }

        public DbCommand SelectSnapshot(string persistenceId, long sequenceNr, DateTime maxTimestamp)
        {
            var selectSnapshot = @"SELECT PersistenceId, SequenceNr, Timestamp, Manifest, Snapshot FROM {0}.{1} WHERE PersistenceId = :PersistenceId AND ROWNUM <= 1"
                .QuoteSchemaAndTable(schemaName, tableName);

            var oracleCommand = new OracleCommand();
            oracleCommand.Parameters.Add(new OracleParameter(":PersistenceId", OracleDbType.NVarchar2, persistenceId.Length) { Value = persistenceId });

            var sb = new StringBuilder(selectSnapshot);
            if (sequenceNr > 0 && sequenceNr < long.MaxValue)
            {
                sb.Append(" AND SequenceNr <= :SequenceNr");
                oracleCommand.Parameters.Add(new OracleParameter(":SequenceNr", OracleDbType.Int64) { Value = sequenceNr });
            }

            if (maxTimestamp > DateTime.MinValue && maxTimestamp < DateTime.MaxValue)
            {
                sb.Append(" AND Timestamp <= :Timestamp");
                oracleCommand.Parameters.Add(new OracleParameter(":Timestamp", OracleDbType.TimeStamp) { Value = maxTimestamp });
            }

            sb.Append(" ORDER BY SequenceNr DESC");
            oracleCommand.CommandText = sb.ToString();

            return oracleCommand;
        }

        public DbCommand InsertSnapshot(SnapshotEntry entry)
        {
            var upsertSnapshotSql = @"
MERGE INTO {0}.{1} USING DUAL ON (PersistenceId = :PersistenceId AND SequenceNr = :SequenceNr)
WHEN MATCHED THEN UPDATE SET Timestamp = :Timestamp, Snapshot = :Snapshot
WHEN NOT MATCHED THEN INSERT (PersistenceId, SequenceNr, Timestamp, Manifest, Snapshot) VALUES (:PersistenceId, :SequenceNr, :Timestamp, :Manifest, :Snapshot)".QuoteSchemaAndTable(schemaName, tableName);

            var oracleCommand = new OracleCommand(upsertSnapshotSql)
            {
                BindByName = true,
                Parameters =
                {
                    new OracleParameter(":PersistenceId", OracleDbType.NVarchar2, entry.PersistenceId.Length) { Value = entry.PersistenceId },
                    new OracleParameter(":SequenceNr", OracleDbType.Int64) { Value = entry.SequenceNr },
                    new OracleParameter(":Timestamp", OracleDbType.TimeStamp) { Value = entry.Timestamp },
                    new OracleParameter(":Manifest", OracleDbType.NVarchar2, entry.SnapshotType.Length) { Value = entry.SnapshotType },
                    new OracleParameter(":Snapshot", OracleDbType.Blob, entry.Snapshot.Length) { Value = entry.Snapshot }
                }
            };

            return oracleCommand;
        }
    }
}