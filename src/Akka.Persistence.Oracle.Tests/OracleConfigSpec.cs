using System;
using Xunit;

namespace Akka.Persistence.Oracle.Tests
{
    public class OracleConfigSpec : Akka.TestKit.Xunit2.TestKit
    {
        [Fact]
        public void Should_Oracle_journal_has_default_config()
        {
            OraclePersistence.Get(Sys);

            var config = Sys.Settings.Config.GetConfig("akka.persistence.journal.oracle");

            Assert.NotNull(config);
            Assert.Equal("akka.persistence.journal.oracle", Sys.Settings.Config.GetString("akka.persistence.journal.plugin"));
            Assert.Equal("Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle", config.GetString("class"));
            Assert.Equal("akka.actor.default-dispatcher", config.GetString("plugin-dispatcher"));
            Assert.Equal(string.Empty, config.GetString("connection-string"));
            Assert.Equal(string.Empty, config.GetString("connection-string-name"));
            Assert.Equal(TimeSpan.FromSeconds(30), config.GetTimeSpan("connection-timeout"));
            Assert.Equal("EVENTJOURNAL", config.GetString("table-name"));
            Assert.Equal("METADATA", config.GetString("metadata-table-name"));
            Assert.Equal(false, config.GetBoolean("auto-initialize"));
            Assert.Equal("Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, Akka.Persistence.Sql.Common", config.GetString("timestamp-provider"));
        }

        [Fact]
        public void Should_Oracle_snapshot_has_default_config()
        {
            OraclePersistence.Get(Sys);

            var config = Sys.Settings.Config.GetConfig("akka.persistence.snapshot-store.oracle");

            Assert.NotNull(config);
            Assert.Equal("akka.persistence.snapshot-store.oracle", Sys.Settings.Config.GetString("akka.persistence.snapshot-store.plugin"));
            Assert.Equal("Akka.Persistence.Oracle.Snapshot.OracleSnapshotStore, Akka.Persistence.Oracle", config.GetString("class"));
            Assert.Equal("akka.actor.default-dispatcher", config.GetString("plugin-dispatcher"));
            Assert.Equal(string.Empty, config.GetString("connection-string"));
            Assert.Equal(string.Empty, config.GetString("connection-string-name"));
            Assert.Equal(TimeSpan.FromSeconds(30), config.GetTimeSpan("connection-timeout"));
            Assert.Equal("SNAPSHOTSTORE", config.GetString("table-name"));
            Assert.Equal(false, config.GetBoolean("auto-initialize"));
        }
    }
}
