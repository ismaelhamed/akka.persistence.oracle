//-----------------------------------------------------------------------
// <copyright file="BatchingOracleCurrentEventsByTagSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Query.Sql;
using Akka.Persistence.TCK.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Batching
{
    [Collection("OracleSpec")]
    public class BatchingOracleCurrentEventsByTagSpec : CurrentEventsByTagSpec
    {
        public static Config Config => ConfigurationFactory.ParseString(@"
            akka.test.single-expect-default = 30s
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        event-adapters {
                            color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                        }
                        event-adapter-bindings = {
                            ""System.String"" = color-tagger
                        }
                        class = ""Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                        refresh-interval = 1s
                    }
                }
            }").WithFallback(SqlReadJournal.DefaultConfiguration());
        
        static BatchingOracleCurrentEventsByTagSpec()
        {
            DbUtils.Initialize();
        }
        
        public BatchingOracleCurrentEventsByTagSpec(ITestOutputHelper output)
            : base(Config, nameof(BatchingOracleCurrentEventsByTagSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }

        [Fact(Skip="Batching journal does not support this operation. See PR #3698")]
        public override void ReadJournal_query_CurrentEventsByTag_should_see_all_150_events() => 
            base.ReadJournal_query_CurrentEventsByTag_should_see_all_150_events();
    }
}