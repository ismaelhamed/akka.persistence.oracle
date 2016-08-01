//-----------------------------------------------------------------------
// <copyright file="OracleEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Query.Sql;
using Akka.Persistence.Sql.TestKit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Query
{
    public class OracleEventsByPersistenceIdSpec : EventsByPersistenceIdSpec
    {
        public static Config Config => ConfigurationFactory.ParseString(@"
            akka.loglevel = INFO
            akka.test.single-expect-default = 10s
            akka.persistence.journal.plugin = ""akka.persistence.journal.oracle""
            akka.persistence.journal.oracle {
                class = ""Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle""
                plugin-dispatcher = ""akka.actor.default-dispatcher""
                table-name = EVENTJOURNAL
                schema-name = AKKA_PERSISTENCE_TEST
                auto-initialize = on
                connection-string = ""TestDb""
                refresh-interval = 1s
            }")
            .WithFallback(SqlReadJournal.DefaultConfiguration());

        public OracleEventsByPersistenceIdSpec(ITestOutputHelper output)
            : base(Config, output)
        { }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}
