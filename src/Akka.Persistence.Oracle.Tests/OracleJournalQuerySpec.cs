//-----------------------------------------------------------------------
// <copyright file="OracleJournalQuerySpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Sql.TestKit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests
{
    [Collection("OracleSpec")]
    public class OracleJournalQuerySpec : SqlJournalQuerySpec
    {
        private static readonly Config SpecConfig;

        static OracleJournalQuerySpec()
        {
            SpecConfig = ConfigurationFactory.ParseString(@"
                akka.test.single-expect-default = 10s
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
                } " + TimestampConfig("akka.persistence.journal.oracle"));
        }

        public OracleJournalQuerySpec(ITestOutputHelper output)
            : base(SpecConfig, "OracleJournalQuerySpec", output)
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
