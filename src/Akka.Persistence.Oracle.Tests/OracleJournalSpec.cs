using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.TestKit.Journal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests
{
    [Collection("OracleSpec")]
    public class OracleJournalSpec : JournalSpec
    {
        private static readonly Config SpecConfig;

        static OracleJournalSpec()
        {
            const string SpecString = @"
                akka.test.single-expect-default = 15s
                akka.persistence {
                    publish-plugin-commands = on
                    journal {
                        plugin = ""akka.persistence.journal.oracle""
                        oracle {
                            class = ""Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle""                            
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            table-name = EVENTJOURNAL
                            schema-name = AKKA_PERSISTENCE_TEST
                            auto-initialize = on
                            connection-string-name = ""TestDb""
                        }
                    }
                }";

            SpecConfig = ConfigurationFactory.ParseString(SpecString);
            DbUtils.Clean();
        }

        public OracleJournalSpec(ITestOutputHelper output)
            : base(SpecConfig, "OracleJournalSpec", output)
        {
            Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }

        [Fact]
        public new void Journal_should_not_reset_HighestSequenceNr_after_journal_cleanup()
        {
            var receiverProbe = CreateTestProbe();
            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, Pid, receiverProbe.Ref));
            for (var i = 1; i <= 5; i++)
            {
                var seqNr = i;
                receiverProbe.ExpectMsg<ReplayedMessage>(m => IsReplayedMessage(m, seqNr));
            }
            receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);

            Journal.Tell(new DeleteMessagesTo(Pid, long.MaxValue, receiverProbe.Ref));
            receiverProbe.ExpectMsg<DeleteMessagesSuccess>(m => m.ToSequenceNr == long.MaxValue);

            Journal.Tell(new ReplayMessages(0, long.MaxValue, long.MaxValue, Pid, receiverProbe.Ref));
            receiverProbe.ExpectMsg<RecoverySuccess>(m => m.HighestSequenceNr == 5L);
        }
    }
}
