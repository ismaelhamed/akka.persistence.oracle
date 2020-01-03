# Akka.Persistence.Oracle

Akka.NET Persistence journal and snapshot store backed by Oracle ODP.NET

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/d9fa5125e8284a4d8a6b9e04b355fd1f)](https://app.codacy.com/app/ismaelhamed/akka.persistence.oracle?utm_source=github.com&utm_medium=referral&utm_content=ismaelhamed/akka.persistence.oracle&utm_campaign=Badge_Grade_Dashboard)
[![Build Status](https://dev.azure.com/ismaelhamed/akka.persistence.oracle/_apis/build/status/akka.persistence.oracle-CI)](https://dev.azure.com/ismaelhamed/akka.persistence.oracle/_build/latest?definitionId=9)
[![NuGet](https://img.shields.io/nuget/v/Akka.Persistence.Oracle.svg)](https://www.nuget.org/packages/Akka.Persistence.Oracle/)
![Downloads](https://img.shields.io/nuget/dt/Akka.Persistence.Oracle.svg)
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fismaelhamed%2Fakka.persistence.oracle.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2Fismaelhamed%2Fakka.persistence.oracle?ref=badge_shield)

## Configuration

Both journal and snapshot store share the same configuration keys (however they resides in separate scopes, so they are defined distinctly for either journal or snapshot store):

Remember that connection string must be provided separately to Journal and Snapshot Store.

```hocon
akka.persistence {

    journal {
        plugin = "akka.persistence.journal.oracle"
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
        plugin = "akka.persistence.snapshot-store.oracle"
        oracle {
            # qualified type name of the Oracle persistence journal actor
            class = "Akka.Persistence.Oracle.Snapshot.OracleSnapshotStore, Akka.Persistence.Oracle"

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
            table-name = SNAPSHOTSTORE

            # should corresponding journal table be initialized automatically
            auto-initialize = off
        }
    }
}
```

### Batching journal

Since version 1.2 an alternative, experimental type of the journal has been released, known as batching journal. It's optimized for concurrent writes made by multiple persistent actors, thanks to the ability of batching multiple SQL operations to be executed within the same database connection. In some of those situations we've noticed over an order of magnitude in event write speed.

To use batching journal, simply change `akka.persistence.journal.oracle.class` to *Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle*.

Additionally to the existing settings, batching journal introduces few more:

- `isolation-level` to define isolation level for transactions used withing event reads/writes. Possible options: *read-committed* (default).
- `max-concurrent-operations` is used to limit the maximum number of database connections used by this journal. You can use them in situations when you want to partition the same `ADO.NET` pool between multiple components. Current default: *64*.
- `max-batch-size` defines the maximum number of SQL operations, that are allowed to be executed using the same connection. When there are more operations, they will chunked into subsequent connections. Current default: *100*.
- `max-buffer-size` defines maximum buffer capacity for the requests send to a journal. Once buffer gets overflown, a journal will call `OnBufferOverflow` method. By default it will reject all incoming requests until the buffer space gets freed. You can inherit from `BatchingOracleJournal` and override that method to provide a custom back-pressure strategy. Current default: *500 000*.

### Table Schema

Oracle persistence plugin defines a default table schema used for journal, snapshot store and metadata table.

```sql
CREATE TABLE EVENTJOURNAL (
    Ordering INTEGER NOT NULL,
    PersistenceId NVARCHAR2(255) NOT NULL,
    SequenceNr NUMBER(19,0) NOT NULL,
    Timestamp NUMBER(19,0) NOT NULL,
    IsDeleted NUMBER(1,0) DEFAULT(0) NOT NULL CHECK (IsDeleted IN (0,1)),
    Manifest NVARCHAR2(500) NOT NULL,
    Payload BLOB NOT NULL,
    Tags NVARCHAR2(100) NULL,
    SerializerId NUMBER(10,0) NULL,
    CONSTRAINT PK_EVENTJOURNAL PRIMARY KEY (Ordering),
    CONSTRAINT QU_EVENTJOURNAL UNIQUE (PersistenceId, SequenceNr)
);

CREATE INDEX IX_EVENTJOURNAL_SequenceNr ON EVENTJOURNAL(SequenceNr);
CREATE INDEX IX_EVENTJOURNAL_Timestamp ON EVENTJOURNAL(Timestamp);

CREATE SEQUENCE EVENTJOURNAL_SEQ
    START WITH 1
    INCREMENT BY 1
    CACHE 1000
    ORDER
    NOCYCLE
    NOMAXVALUE;

CREATE OR REPLACE TRIGGER EVENTJOURNAL_TRG
BEFORE INSERT ON EVENTJOURNAL
FOR EACH ROW
BEGIN
    :new.Ordering := EVENTJOURNAL_SEQ.NEXTVAL;
END;

/

ALTER TRIGGER EVENTJOURNAL_TRG ENABLE;

CREATE TABLE METADATA (
    PersistenceId NVARCHAR2(255) NOT NULL,
    SequenceNr NUMBER(19,0) NOT NULL,
    CONSTRAINT PK_METADATA PRIMARY KEY (PersistenceId, SequenceNr)
);

CREATE TABLE SNAPSHOTSTORE (
    PersistenceId NVARCHAR2(255) NOT NULL,
    SequenceNr NUMBER(19,0) NOT NULL,
    Timestamp TIMESTAMP(7) NOT NULL,
    Manifest NVARCHAR2(500) NOT NULL,
    Snapshot BLOB NOT NULL,
    SerializerId NUMBER(10,0) NULL,
    CONSTRAINT PK_SNAPSHOTSTORE PRIMARY KEY (PersistenceId, SequenceNr)
);

CREATE INDEX IX_SNAPSHOTSTORE_SequenceNr ON SNAPSHOTSTORE(SequenceNr);
CREATE INDEX IX_SNAPSHOTSTORE_Timestamp ON SNAPSHOTSTORE(Timestamp);
CREATE INDEX IX_SNAPSHOTSTORE_03 ON SNAPSHOTSTORE(Timestamp, SequenceNr DESC, PersistenceId);
```

### Migration

#### From 1.3.1 to 1.3.10

```sql
ALTER TABLE EVENTJOURNAL ADD CONSTRAINT PK_EVENTJOURNAL PRIMARY KEY (ORDERING);
```

#### From 1.1.2 to 1.3.1

```sql
ALTER TABLE {your_journal_table_name} ADD SerializerId NUMBER(10,0) NULL;
ALTER TABLE {your_snapshot_table_name} ADD SerializerId NUMBER(10,0) NULL;

CREATE INDEX IX_SNAPSHOTSTORE_03 ON {your_snapshot_table_name}(Timestamp, SequenceNr DESC, PersistenceId);
```

### Preparing the test environment

In order to run the tests, you must do the following things:

1. Download and install Docker for Windows from: <https://docs.docker.com/docker-for-windows/>
2. Get Oracle Express 11g R2 on Ubuntu 16.04 LTS from: <https://hub.docker.com/r/wnameless/oracle-xe-11g/>
3. Run the following script to create the proper user and schema:

    ```sql
    CREATE USER AKKA_PERSISTENCE_TEST IDENTIFIED BY akkadotnet;
    GRANT CREATE SESSION TO AKKA_PERSISTENCE_TEST;
    GRANT CREATE TABLE TO AKKA_PERSISTENCE_TEST;
    GRANT CREATE VIEW TO AKKA_PERSISTENCE_TEST;
    GRANT CREATE SEQUENCE TO AKKA_PERSISTENCE_TEST;
    GRANT CREATE TRIGGER TO AKKA_PERSISTENCE_TEST;

    ALTER USER AKKA_PERSISTENCE_TEST QUOTA UNLIMITED ON USERS;
    ALTER USER AKKA_PERSISTENCE_TEST DEFAULT TABLESPACE USERS;
    ```

4. The default connection string uses the following credentials: `Data Source=192.168.99.100:1521/XE;User Id=AKKA_PERSISTENCE_TEST;Password=akkadotnet;`
5. A custom app.config file can be used and needs to be placed in the same folder as the dll

### Running the tests

The Oracle tests are packaged and run as part of the "RunTests" and "All" build tasks. Run the following command from the PowerShell command line:

```powershell
PS> .\build RunTests
```

## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fismaelhamed%2Fakka.persistence.oracle.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2Fismaelhamed%2Fakka.persistence.oracle?ref=badge_large)