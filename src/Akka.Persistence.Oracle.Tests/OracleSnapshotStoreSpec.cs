//-----------------------------------------------------------------------
// <copyright file="OracleJournalPerfSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Oracle.Tests
{
    [Collection("OracleSpec")]
    public class OracleSnapshotStoreSpec : SnapshotStoreSpec
    {
        public static Config SpecConfig => ConfigurationFactory.ParseString(@"
            akka.test.single-expect-default = 10s
            akka.persistence {
                publish-plugin-commands = on
                snapshot-store {
                    plugin = ""akka.persistence.snapshot-store.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Snapshot.OracleSnapshotStore, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        table-name = SNAPSHOTSTORE
                        auto-initialize = on
                        connection-string = """ + DbUtils.ConnectionString + @"""
                    }
                }
            }");

        static OracleSnapshotStoreSpec()
        {
            DbUtils.Initialize();
        }

        public OracleSnapshotStoreSpec(ITestOutputHelper output)
            : base(SpecConfig, "OracleSnapshotStoreSpec", output)
        {
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}