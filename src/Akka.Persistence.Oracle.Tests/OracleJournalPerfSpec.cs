// //-----------------------------------------------------------------------
// // <copyright file="OracleJournalPerfSpec.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.TestKit.Performance;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests
{
    [Collection("OracleJournalPerfSpec")]
    public class OracleJournalPerfSpec : JournalPerfSpec
    {
        public static Config SpecConfig => ConfigurationFactory.ParseString(@"
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
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
            }");
        
        static OracleJournalPerfSpec()
        {
            DbUtils.Initialize();
        }

        public OracleJournalPerfSpec(ITestOutputHelper output)
            : base(SpecConfig, "OracleJournalPerfSpec", output)
        {
            EventsCount = 1000;
            ExpectDuration = TimeSpan.FromMinutes(10);
            MeasurementIterations = 10;
        }

        protected override void AfterAll()
        {
            base.AfterAll();
            DbUtils.Clean();
        }
    }
}