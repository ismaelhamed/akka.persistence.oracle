//-----------------------------------------------------------------------
// <copyright file="BatchingSqlServerJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Query.Sql;
using Akka.Persistence.Sql.TestKit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Batching
{
    [Collection("OracleSpec")]
    public class BatchingOracleAllPersistenceIdsSpec : AllPersistenceIdsSpec
    {
        public static Config Config => ConfigurationFactory.ParseString(@"
            akka.test.single-expect-default = 10s
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        schema-name = AKKA_PERSISTENCE_TEST
                        auto-initialize = on
                        connection-string-name = ""TestDb""
                        refresh-interval = 1s
                    }
                }
            }").WithFallback(SqlReadJournal.DefaultConfiguration());

        public BatchingOracleAllPersistenceIdsSpec(ITestOutputHelper output)
            : base(Config, output)
        { }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}
