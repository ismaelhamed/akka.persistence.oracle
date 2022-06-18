//-----------------------------------------------------------------------
// <copyright file="BatchingSqlServerJournalSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Query.Sql;
using Akka.Persistence.TCK.Query;
using Akka.Streams.TestKit;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Batching
{
    [Collection("OracleSpec")]
    public class BatchingOraclePersistenceIdsSpec : PersistenceIdsSpec
    {
        public static Config Config => ConfigurationFactory.ParseString(@"
            akka.loglevel = DEBUG            
            akka.actor {
                serializers{
                    persistence-tck-test = ""Akka.Persistence.TCK.Serialization.TestSerializer,Akka.Persistence.TCK""
                }
                serialization-bindings {
                    ""Akka.Persistence.TCK.Serialization.TestPayload,Akka.Persistence.TCK"" = persistence-tck-test
                }
            }
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
                query.journal.sql.refresh-interval = 1s
            }
            akka.test.single-expect-default = 10s")
            .WithFallback(SqlReadJournal.DefaultConfiguration());

        static BatchingOraclePersistenceIdsSpec()
        {
            DbUtils.Initialize();
        }

        public BatchingOraclePersistenceIdsSpec(ITestOutputHelper output)
            : base(Config, nameof(BatchingOraclePersistenceIdsSpec), output)
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