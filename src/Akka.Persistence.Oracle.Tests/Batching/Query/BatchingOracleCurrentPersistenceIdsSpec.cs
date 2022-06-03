//-----------------------------------------------------------------------
// <copyright file="BatchingOracleCurrentPersistenceIdsSpec.cs" company="Akka.NET Project">
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
    public class BatchingOracleCurrentPersistenceIdsSpec : CurrentPersistenceIdsSpec
    {
        public static Config Config => ConfigurationFactory.ParseString(@"
            akka.loglevel = DEBUG
            akka.test.single-expect-default = 10s
            akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                        refresh-interval = 1s
                    }
                }
            }").WithFallback(SqlReadJournal.DefaultConfiguration());
        
        static BatchingOracleCurrentPersistenceIdsSpec()
        {
            DbUtils.Initialize();
        }
        
        public BatchingOracleCurrentPersistenceIdsSpec(ITestOutputHelper output)
            : base(Config, nameof(BatchingOracleCurrentPersistenceIdsSpec), output)
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