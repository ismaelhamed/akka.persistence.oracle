using Akka.Configuration;
using Akka.Persistence.TestKit.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests
{
    [Collection("OracleSpec")]
    public class OracleSnapshotStoreSpec : SnapshotStoreSpec
    {
        private static readonly Config SpecConfig;

        static OracleSnapshotStoreSpec()
        {
            const string SpecString = @"
                akka.persistence {
                    publish-plugin-commands = on
                    snapshot-store {
                        plugin = ""akka.persistence.snapshot-store.oracle""
                        oracle {
                            class = ""Akka.Persistence.Oracle.Snapshot.OracleSnapshotStore, Akka.Persistence.Oracle""
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            table-name = SNAPSHOTSTORE
                            schema-name = AKKA_PERSISTENCE_TEST
                            auto-initialize = on
                            connection-string-name = ""TestDb""
                        }
                    }
                }";

            SpecConfig = ConfigurationFactory.ParseString(SpecString);
            DbUtils.Clean();
        }

        public OracleSnapshotStoreSpec(ITestOutputHelper output)
            : base(SpecConfig, "OracleSnapshotStoreSpec", output)
        {
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }

        [Fact]
        public new void SnapshotStore_should_save_and_overwrite_snapshot_with_same_sequence_number()
        {
            var senderProbe = CreateTestProbe();
            var md = Metadata[4];
            SnapshotStore.Tell(new SaveSnapshot(md, "s-5-modified"), senderProbe.Ref);

            var md2 = senderProbe.ExpectMsg<SaveSnapshotSuccess>().Metadata;
            Assert.Equal(md.SequenceNr, md2.SequenceNr);
            SnapshotStore.Tell(new LoadSnapshot(Pid, new SnapshotSelectionCriteria(md.SequenceNr), long.MaxValue), senderProbe.Ref);

            var result = senderProbe.ExpectMsg<LoadSnapshotResult>();
            Assert.Equal("s-5-modified", result.Snapshot.Snapshot.ToString());
            Assert.Equal(md.SequenceNr, result.Snapshot.Metadata.SequenceNr);
            // metadata timestamp may have been changed
        }
    }
}
