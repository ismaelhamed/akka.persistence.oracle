﻿//-----------------------------------------------------------------------
// <copyright file="SqlServerEventsByTagSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
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
    public class OracleEventsByTagSpec : EventsByTagSpec
    {
        private static Config Config => ConfigurationFactory.ParseString(@"
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
                        table-name = EVENTJOURNAL
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
                query.journal.sql.refresh-interval = 1s
            }").WithFallback(SqlReadJournal.DefaultConfiguration());

        static OracleEventsByTagSpec()
        {
            DbUtils.Initialize();
        }

        public OracleEventsByTagSpec(ITestOutputHelper output)
            : base(Config, nameof(OracleEventsByTagSpec), output)
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
