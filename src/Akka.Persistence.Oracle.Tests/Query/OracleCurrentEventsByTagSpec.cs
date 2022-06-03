//-----------------------------------------------------------------------
// <copyright file="OracleCurrentEventsByTagSpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Oracle.Tests.Query
{
    [Collection("OracleSpec")]
    public class OracleCurrentEventsByTagSpec : CurrentEventsByTagSpec
    {
        public static Config Config => ConfigurationFactory.ParseString(@"
            akka.test.single-expect-default = 10s
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
                        class = ""Akka.Persistence.Oracle.Journal.OracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                        refresh-interval = 1s
                    }
                }
            }").WithFallback(SqlReadJournal.DefaultConfiguration());
        
        static OracleCurrentEventsByTagSpec()
        {
            DbUtils.Initialize();
        }
        
        public OracleCurrentEventsByTagSpec(ITestOutputHelper output)
            : base(Config, nameof(OracleCurrentEventsByTagSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}