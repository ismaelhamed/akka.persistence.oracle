// //-----------------------------------------------------------------------
// // <copyright file="BatchingOracleJournalPerfSpec.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.TestKit.Performance;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests.Batching
{
    [Collection("OracleSpec")]
    public class BatchingOracleJournalPerfSpec : JournalPerfSpec
    {
        public static Config SpecConfig => ConfigurationFactory.ParseString(@"
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle""                            
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        table-name = EVENTJOURNAL
                        schema-name = AKKA_PERSISTENCE_TEST
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
            }");
        
        static BatchingOracleJournalPerfSpec()
        {
            DbUtils.Initialize();
        }

        public BatchingOracleJournalPerfSpec(ITestOutputHelper output)
            : base(SpecConfig, "BatchingOracleJournalPerfSpec", output)
        {
            EventsCount = 1000;
            ExpectDuration = TimeSpan.FromMinutes(10);
            MeasurementIterations = 1;
        }

        protected override void AfterAll()
        {
            base.AfterAll();
            DbUtils.Clean();
        }
    }
}