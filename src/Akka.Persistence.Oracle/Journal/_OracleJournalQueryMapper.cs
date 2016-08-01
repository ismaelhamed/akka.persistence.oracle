using System;
using System.Data.Common;
using Akka.Actor;
using Akka.Persistence.Sql.Common.Journal;

namespace Akka.Persistence.Oracle.Journal
{
    internal class OracleJournalQueryMapper : IJournalQueryMapper
    {
        public const int PersistenceIdIndex = 0;
        public const int SequenceNrIndex = 1;
        public const int IsDeletedIndex = 2;
        public const int ManifestIndex = 3;
        public const int PayloadIndex = 4;

        private readonly Akka.Serialization.Serialization serialization;

        public OracleJournalQueryMapper(Akka.Serialization.Serialization serialization)
        {
            this.serialization = serialization;
        }

        public IPersistentRepresentation Map(DbDataReader reader, IActorRef sender = null)
        {
            var persistenceId = reader.GetString(PersistenceIdIndex);
            var sequenceNr = reader.GetInt64(SequenceNrIndex);
            var isDeleted = Convert.ToBoolean(reader.GetInt16(IsDeletedIndex)) ;
            var manifest = reader.GetString(ManifestIndex);
            // timestamp is SQL-journal specific field, it's not a part of casual Persistent instance  
            var payload = GetPayload(reader, manifest);

            return new Persistent(payload, sequenceNr, persistenceId, manifest, isDeleted, sender);
        }

        private object GetPayload(DbDataReader reader, string manifest)
        {
            var type = Type.GetType(manifest, true);
            var binary = (byte[])reader[PayloadIndex];

            var serializer = serialization.FindSerializerForType(type);
            return serializer.FromBinary(binary, type);
        }
    }
}