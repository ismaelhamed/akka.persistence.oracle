using Akka.Configuration;
using Akka.Persistence.TCK.Serialization;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Serialization
{
    public class OracleSnapshotSerializationSpec : SnapshotStoreSerializationSpec
    {
        private static Config Config => ConfigurationFactory.ParseString(@"
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

        public OracleSnapshotSerializationSpec(ITestOutputHelper output)
            : base(Config, nameof(OracleSnapshotSerializationSpec), output)
        { }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}
