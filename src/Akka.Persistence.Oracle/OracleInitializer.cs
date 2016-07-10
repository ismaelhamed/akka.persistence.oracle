using System;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle
{
    internal static class OracleInitializer
    {
        private const string OracleJournalFormat = @"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{2}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE(
            'CREATE TABLE {0}.{1} (
              PersistenceID NVARCHAR2(255) NOT NULL,
              SequenceNr NUMBER(19,0) NOT NULL,
              Timestamp TIMESTAMP(7) NOT NULL,
              IsDeleted NUMBER(1,0) DEFAULT(0) NOT NULL CHECK (IsDeleted IN (1,0)),
              Manifest NVARCHAR2(500) NOT NULL,
              Payload BLOB NOT NULL,
              CONSTRAINT PK_{2} PRIMARY KEY (PersistenceID, SequenceNr)
            )'
        );
        EXECUTE IMMEDIATE ('CREATE INDEX IX_{2}_SequenceNr ON {1}(SequenceNr)');
        EXECUTE IMMEDIATE ('CREATE INDEX IX_{2}_Timestamp ON {1}(Timestamp)');       
    END IF;
END;";

        private const string OracleSnapshotStoreFormat = @"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{2}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE(
            'CREATE TABLE {0}.{1} (
              PersistenceID NVARCHAR2(255) NOT NULL,
              SequenceNr NUMBER(19,0) NOT NULL,
              Timestamp TIMESTAMP(7) NOT NULL,
              Manifest NVARCHAR2(500) NOT NULL,
              Snapshot BLOB NOT NULL,
              CONSTRAINT PK_{2} PRIMARY KEY (PersistenceID, SequenceNr)
            )'
        );
        EXECUTE IMMEDIATE ('CREATE INDEX IX_{2}_SequenceNr ON {1}(SequenceNr)');
        EXECUTE IMMEDIATE ('CREATE INDEX IX_{2}_Timestamp ON {1}(Timestamp)');         
    END IF;
END;";

        private const string OracleMetadataFormat = @"
DECLARE
    table_count integer;
BEGIN    
    SELECT COUNT (OBJECT_ID) INTO table_count 
    FROM USER_OBJECTS 
    WHERE EXISTS (SELECT OBJECT_NAME FROM USER_OBJECTS WHERE (OBJECT_NAME = UPPER('{2}') AND OBJECT_TYPE = 'TABLE'));

    IF table_count = 0 THEN 
        EXECUTE IMMEDIATE(
            'CREATE TABLE {0}.{1} (
              PersistenceID NVARCHAR2(255) NOT NULL,
              SequenceNr NUMBER(19,0) NOT NULL,
              CONSTRAINT PK_{2} PRIMARY KEY (PersistenceID, SequenceNr)
            )'
        );        
    END IF;
END;";

        /// <summary>
        /// Initializes a Oracle journal-related tables according to 'schema-name', 'table-name' 
        /// and 'connection-string' values provided in 'akka.persistence.journal.oracle' config.
        /// </summary>
        internal static void CreateOracleJournalTables(string connectionString, string schemaName, string tableName)
        {
            try
            {
                var sql = InitJournalSql(tableName, schemaName);
                ExecuteSql(connectionString, sql);
            }
            catch (OracleException ex)
            {
                if (ex.HResult != 955)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Initializes a Oracle snapshot store related tables according to 'schema-name', 'table-name' 
        /// and 'connection-string' values provided in 'akka.persistence.snapshot-store.oracle' config.
        /// </summary>
        internal static void CreateOracleSnapshotStoreTables(string connectionString, string schemaName, string tableName)
        {
            try
            {
                var sql = InitSnapshotStoreSql(tableName, schemaName);
                ExecuteSql(connectionString, sql);
            }
            catch (OracleException ex)
            {
                if (ex.HResult != 955)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Initializes a Oracle metadata table according to 'schema-name', 'metadata-table-name' 
        /// and 'connection-string' values provided in 'akka.persistence.snapshot-store.oracle' config.
        /// </summary>
        internal static void CreateOracleMetadataTables(string connectionString, string schemaName, string metadataTableName)
        {
            try
            {
                var sql = InitMetadataSql(metadataTableName, schemaName);
                ExecuteSql(connectionString, sql);
            }
            catch (OracleException ex)
            {
                if (ex.HResult != 955)
                {
                    throw;
                }
            }
        }

        private static string InitJournalSql(string tableName, string schemaName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName), "Akka.Persistence.Oracle journal table name is required");

            var builder = new OracleCommandBuilder();
            return string.Format(OracleJournalFormat, builder.QuoteIdentifier(schemaName), builder.QuoteIdentifier(tableName), builder.SafeUnquoteIdentifier(tableName));
        }

        private static string InitMetadataSql(string metadataTable, string schemaName)
        {
            if (string.IsNullOrEmpty(metadataTable))
                throw new ArgumentNullException(nameof(metadataTable), "Akka.Persistence.Oracle metadata table name is required");

            var builder = new OracleCommandBuilder();
            return string.Format(OracleMetadataFormat, builder.QuoteIdentifier(schemaName), builder.QuoteIdentifier(metadataTable), builder.SafeUnquoteIdentifier(metadataTable));
        }

        private static string InitSnapshotStoreSql(string tableName, string schemaName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName), "Akka.Persistence.Oracle snapshot store table name is required");

            var builder = new OracleCommandBuilder();
            return string.Format(OracleSnapshotStoreFormat, builder.QuoteIdentifier(schemaName), builder.QuoteIdentifier(tableName), builder.SafeUnquoteIdentifier(tableName));
        }

        private static void ExecuteSql(string connectionString, string sql)
        {
            using (var connection = new OracleConnection(connectionString))
            using (var command = connection.CreateCommand())
            {
                connection.Open();

                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}