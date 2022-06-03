using Akka.Configuration;
using Akka.Persistence.TCK.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Serialization
{
    [Collection("OracleSpec")]
    public class OracleJournalSerializationSpec : JournalSerializationSpec
    {
        private static Config Config => ConfigurationFactory.ParseString(@"
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        event-adapters {
                            custom-adapter = ""Akka.Persistence.TCK.Serialization.TestJournal+MyWriteAdapter, Akka.Persistence.TCK""
                        }
                        event-adapter-bindings = {
                            ""Akka.Persistence.TCK.Serialization.TestJournal+MyPayload3, Akka.Persistence.TCK"" = custom-adapter
                        }    
                        class = ""Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        table-name = EVENTJOURNAL
                        metadata-table-name = METADATA
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
            }");

        static OracleJournalSerializationSpec()
        {
            DbUtils.Initialize();
        }

        public OracleJournalSerializationSpec(ITestOutputHelper output)
            : base(Config, nameof(OracleJournalSerializationSpec), output)
        { }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }

        [Fact(Skip = "Sql plugin does not support EventAdapter.Manifest")]
        public override void Journal_should_serialize_Persistent_with_EventAdapter_manifest()
        { }
    }
}