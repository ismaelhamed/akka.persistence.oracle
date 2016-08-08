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
            SpecConfig = ConfigurationFactory.ParseString(@"
                akka.test.single-expect-default = 10s
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
                }");
        }

        public OracleSnapshotStoreSpec(ITestOutputHelper output)
            : base(SpecConfig, "OracleSnapshotStoreSpec", output)
        {
            OraclePersistence.Get(Sys);
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}
