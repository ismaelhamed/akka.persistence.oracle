## Akka.Persistence.Oracle

Akka.NET Persistence journal and snapshot store backed by Oracle ODP.NET

**WARNING: Akka.Persistence.Oracle plugin is still in beta and it's mechanics described bellow may be still subject to change**.

### Configuration

Both journal and snapshot store share the same configuration keys (however they resides in separate scopes, so they are definied distinctly for either journal or snapshot store):

Remember that connection string must be provided separately to Journal and Snapshot Store.

```hocon
akka.persistence {

    journal {
        oracle {		
            # qualified type name of the Oracle persistence journal actor
            class = "Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle"

            # dispatcher used to drive journal actor
            plugin-dispatcher = "akka.actor.default-dispatcher"

            # connection string used for database access
            connection-string = ""
			
            # connection string name for .config file used when no connection string has been provided
            connection-string-name = ""			

            # default SQL commands timeout
            connection-timeout = 30s

            # Oracle schema name to table corresponding with persistent journal
            schema-name = SYSTEM

            # Oracle table corresponding with persistent journal
            table-name = EVENTJOURNAL

            # should corresponding journal table be initialized automatically
            auto-initialize = off
			
            # timestamp provider used for generation of journal entries timestamps
            timestamp-provider = "Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, Akka.Persistence.Sql.Common"

            # metadata table
            metadata-table-name = METADATA
        }
    }

    snapshot-store {
        oracle {		
            # qualified type name of the Oracle persistence journal actor
            class = "Akka.Persistence.Oracle.Snapshot.OracleSnapshotStore, Akka.Persistence.Oracle"

            # dispatcher used to drive journal actor
            plugin-dispatcher = ""akka.actor.default-dispatcher""

            # connection string used for database access
            connection-string = ""
			
            # connection string name for .config file used when no connection string has been provided
            connection-string-name = ""			

            # default SQL commands timeout
            connection-timeout = 30s

            # Oracle schema name to table corresponding with persistent journal
            schema-name = SYSTEM

            # Oracle table corresponding with persistent journal
            table-name = SNAPSHOTSTORE

            # should corresponding journal table be initialized automatically
            auto-initialize = off
        }
    }
}
```

### Table Schema

Oracle persistence plugin defines a default table schema used for journal, snapshot store and metadata table.

```SQL
CREATE TABLE {your_journal_table_name} (
    PersistenceID NVARCHAR2(255) NOT NULL,
    SequenceNr NUMBER(19,0) NOT NULL,
    Timestamp TIMESTAMP(7) NOT NULL,
    IsDeleted NUMBER(1,0) DEFAULT(0) NOT NULL CHECK (IsDeleted IN (1,0)),
    Manifest NVARCHAR2(500) NOT NULL,
    Payload BLOB NOT NULL,
    CONSTRAINT PK_{your_journal_table_name} PRIMARY KEY (PersistenceID, SequenceNr)
);

CREATE TABLE {your_snapshot_table_name} (
    PersistenceID NVARCHAR2(255) NOT NULL,
    SequenceNr NUMBER(19,0) NOT NULL,
    Timestamp TIMESTAMP(7) NOT NULL,
    Manifest NVARCHAR2(500) NOT NULL,
    Snapshot BLOB NOT NULL,
    CONSTRAINT PK_{your_snapshot_table_name} PRIMARY KEY (PersistenceID, SequenceNr)
);

CREATE TABLE {your_metadata_table_name} (
    PersistenceID NVARCHAR2(255) NOT NULL,
    SequenceNr NUMBER(19,0) NOT NULL,
    CONSTRAINT PK_{your_metadata_table_name} PRIMARY KEY (PersistenceID, SequenceNr)
);
```

