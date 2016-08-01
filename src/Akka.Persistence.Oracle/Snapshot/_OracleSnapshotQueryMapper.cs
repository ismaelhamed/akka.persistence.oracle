using System;
using System.Data.Common;
using Akka.Persistence.Sql.Common.Snapshot;

namespace Akka.Persistence.Oracle.Snapshot
{
    internal class OracleSnapshotQueryMapper : ISnapshotQueryMapper
    {
        private readonly Akka.Serialization.Serialization serialization;

        public OracleSnapshotQueryMapper(Akka.Serialization.Serialization serialization)
        {
            this.serialization = serialization;
        }

        public SelectedSnapshot Map(DbDataReader reader)
        {
            var persistenceId = reader.GetString(0);
            var sequenceNr = reader.GetInt64(1);
            var timestamp = reader.GetDateTime(2);
            var manifest = reader.GetString(3);

            var metadata = new SnapshotMetadata(persistenceId, sequenceNr, timestamp);
            var snapshot = GetSnapshot(reader, manifest);

            return new SelectedSnapshot(metadata, snapshot);
        }

        private object GetSnapshot(DbDataReader reader, string manifest)
        {
            var type = Type.GetType(manifest, true);
            var binary = (byte[])reader[4];

            var serializer = serialization.FindSerializerForType(type);
            return serializer.FromBinary(binary, type);
        }
    }
}