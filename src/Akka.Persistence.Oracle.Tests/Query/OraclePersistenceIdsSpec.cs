//-----------------------------------------------------------------------
// <copyright file="OracleAllPersistenceIdsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Query.Sql;
using Akka.Persistence.TCK.Query;
using Akka.Streams.TestKit;
using Akka.Util.Internal;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Akka.Persistence.Oracle.Tests.Query
{
    [Collection("OracleSpec")]
    public class OraclePersistenceIdsSpec : PersistenceIdsSpec
    {
        private static Config Config => ConfigurationFactory.ParseString(@"
            akka.loglevel = INFO
            akka.test.single-expect-default = 10s
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        table-name = EVENTJOURNAL
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
                query.journal.sql.refresh-interval = 1s
            }").WithFallback(SqlReadJournal.DefaultConfiguration());

        static OraclePersistenceIdsSpec()
        {
            DbUtils.Initialize();
        }

        public OraclePersistenceIdsSpec(ITestOutputHelper output)
            : base(Config, nameof(OraclePersistenceIdsSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }

        [Fact]
        public override void ReadJournal_AllPersistenceIds_should_find_new_events_after_demand_request()
        {
            var queries = ReadJournal.AsInstanceOf<IPersistenceIdsQuery>();

            Setup("h", 1);
            Setup("i", 1);

            var source = queries.PersistenceIds();
            var probe = source.RunWith(this.SinkProbe<string>(), Materializer);

            probe.Within(TimeSpan.FromSeconds(10), () =>
            {
                probe.Request(1).ExpectNext();
                return probe.ExpectNoMsg(TimeSpan.FromMilliseconds(100));
            });

            Setup("j", 1);
            probe.Within(TimeSpan.FromSeconds(10), () =>
            {
                probe.Request(5).ExpectNext();
                return probe.ExpectNext();
            });
        }
    }
}
