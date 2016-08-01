using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using Akka.Persistence.Sql.Common.Journal;
using Akka.Persistence.Sql.Common.Queries;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Journal
{
    internal class OracleJournalQueryBuilder : IJournalQueryBuilder
    {
        private readonly string tableName;
        private readonly string schemaName;
        private readonly string metadataTableName;

        public OracleJournalQueryBuilder(string tableName, string schemaName, string metadataTableName)
        {
            this.tableName = tableName;
            this.schemaName = schemaName;
            this.metadataTableName = metadataTableName;
        }

        /// <summary>
        /// Returns query which should return events filtered accordingly to provided set of hints.
        /// </summary>
        public DbCommand SelectEvents(IEnumerable<IHint> hints)
        {
            var command = new OracleCommand();

            var sqlized = hints
                .Select(h => HintToSql(h, command))
                .Where(x => !string.IsNullOrEmpty(x));

            var where = string.Join(" AND ", sqlized);
            var sql = new StringBuilder("SELECT PersistenceID, SequenceNr, IsDeleted, Manifest, Payload, Timestamp FROM {0}.{1} ".QuoteSchemaAndTable(schemaName, tableName));
            if (!string.IsNullOrEmpty(where))
            {
                sql.Append(" WHERE ").Append(where);
            }

            command.CommandText = sql.ToString();
            return command;
        }

        /// <summary>
        /// Returns query which should return a frame of messages filtered accordingly to provided parameters.
        /// </summary>
        public DbCommand SelectMessages(string persistenceId, long fromSequenceNr, long toSequenceNr, long max)
        {
            var sql = BuildSelectMessagesSql(fromSequenceNr, toSequenceNr, max);
            var command = new OracleCommand(sql)
            {
                Parameters = { PersistenceIdToSqlParam(persistenceId) }
            };

            return command;
        }

        /// <summary>
        /// Returns query returning single number considered as the highest sequence number in current journal.
        /// </summary>
        public DbCommand SelectHighestSequenceNr(string persistenceId)
        {
            var sb = new StringBuilder("SELECT MAX(SequenceNr) FROM (");
            sb.Append("SELECT SequenceNr FROM {0}.{1} WHERE PersistenceId = :PersistenceId UNION ".QuoteSchemaAndTable(schemaName, metadataTableName));
            sb.Append("SELECT SequenceNr FROM {0}.{1} WHERE PersistenceId = :PersistenceId".QuoteSchemaAndTable(schemaName, tableName));
            sb.Append(")");

            var selectHighestSequenceNrSql = sb.ToString();

            return new OracleCommand(selectHighestSequenceNrSql)
            {
                Parameters = { PersistenceIdToSqlParam(persistenceId) }
            };
        }

        /// <summary>
        /// Returns a non-query command used to insert collection of <paramref name="messages"/> in journal table.
        /// </summary>
        public DbCommand InsertBatchMessages(IPersistentRepresentation[] messages)
        {
            var insertMessagesSql = @"
INSERT INTO {0}.{1} (PersistenceID, SequenceNr, Timestamp, IsDeleted, Manifest, Payload) 
VALUES (:PersistenceId, :SequenceNr, :Timestamp, :IsDeleted, :Manifest, :Payload)".QuoteSchemaAndTable(schemaName, tableName);

            var command = new OracleCommand(insertMessagesSql);
            command.Parameters.Add(":PersistenceId", OracleDbType.NVarchar2);
            command.Parameters.Add(":SequenceNr", OracleDbType.Int64);
            command.Parameters.Add(":Timestamp", OracleDbType.TimeStamp);
            command.Parameters.Add(":IsDeleted", OracleDbType.Int16);
            command.Parameters.Add(":Manifest", OracleDbType.NVarchar2);
            command.Parameters.Add(":Payload", OracleDbType.Blob);

            return command;
        }

        /// <summary>
        /// Returns DELETE statement used to delete rows permanently.
        /// </summary>
        public DbCommand DeleteBatchMessages(string persistenceId, long toSequenceNr)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("DELETE FROM {0}.{1} WHERE PersistenceId = :PersistenceId".QuoteSchemaAndTable(schemaName, tableName));

            if (toSequenceNr != long.MaxValue)
            {
                sqlBuilder.Append(" AND SequenceNr <= ").Append(toSequenceNr);
            }

            return new OracleCommand(sqlBuilder.ToString())
            {
                Parameters = { PersistenceIdToSqlParam(persistenceId) }
            };
        }

        private static string HintToSql(IHint hint, OracleCommand command)
        {
            var timestampRange = hint as TimestampRange;
            if (timestampRange != null)
            {
                var sb = new StringBuilder();

                if (timestampRange.From.HasValue)
                {
                    sb.Append(" Timestamp >= :TimestampFrom ");
                    command.Parameters.Add(new OracleParameter(":TimestampFrom", OracleDbType.TimeStamp).Value = timestampRange.From.Value);
                }

                if (timestampRange.From.HasValue && timestampRange.To.HasValue)
                {
                    sb.Append("AND");
                }

                if (timestampRange.To.HasValue)
                {
                    sb.Append(" Timestamp < :TimestampTo ");
                    command.Parameters.Add(new OracleParameter(":TimestampTo", OracleDbType.TimeStamp).Value = timestampRange.To.Value);
                }

                return sb.ToString();
            }

            var persistenceIdRange = hint as PersistenceIdRange;
            if (persistenceIdRange != null)
            {
                var sb = new StringBuilder(" PersistenceID IN (");

                var i = 0;
                foreach (var persistenceId in persistenceIdRange.PersistenceIds)
                {
                    var paramName = ":PersistenceId" + (i++);
                    sb.Append(paramName).Append(',');
                    command.Parameters.Add(new OracleParameter(paramName, OracleDbType.NVarchar2).Value = persistenceId);
                }

                return persistenceIdRange.PersistenceIds.Count == 0
                    ? string.Empty
                    : sb.Remove(sb.Length - 1, 1).Append(')').ToString();
            }

            var withManifest = hint as WithManifest;
            if (withManifest != null)
            {
                command.Parameters.Add(new OracleParameter(":Manifest", OracleDbType.NVarchar2).Value = withManifest.Manifest);
                return " manifest = :Manifest";
            }

            throw new NotSupportedException($"Oracle journal doesn't support query with hint [{hint.GetType()}]");
        }

        private string BuildSelectMessagesSql(long fromSequenceNr, long toSequenceNr, long max)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append(
                @"SELECT
                    PersistenceID,
                    SequenceNr,
                    IsDeleted,
                    Manifest,
                    Payload ")
                .Append(" FROM {0}.{1} WHERE PersistenceId = :PersistenceId".QuoteSchemaAndTable(schemaName, tableName));

            if (max != long.MaxValue)
            {
                sqlBuilder.Append($" AND ROWNUM <= {max}");
            }

            // since we guarantee type of fromSequenceNr, toSequenceNr and max
            // we can inline them without risk of SQL injection

            if (fromSequenceNr > 0)
            {
                if (toSequenceNr != long.MaxValue)
                    sqlBuilder.Append(" AND SequenceNr BETWEEN ")
                        .Append(fromSequenceNr)
                        .Append(" AND ")
                        .Append(toSequenceNr);
                else
                    sqlBuilder.Append(" AND SequenceNr >= ").Append(fromSequenceNr);
            }

            if (toSequenceNr != long.MaxValue)
                sqlBuilder.Append(" AND SequenceNr <= ").Append(toSequenceNr);

            return sqlBuilder.ToString();
        }

        private static OracleParameter PersistenceIdToSqlParam(string persistenceId, string paramName = null)
        {
            return new OracleParameter(paramName ?? ":PersistenceId", OracleDbType.NVarchar2, persistenceId.Length) { Value = persistenceId };
        }
    }
}