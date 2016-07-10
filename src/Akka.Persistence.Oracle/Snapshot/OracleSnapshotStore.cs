using System.Data.Common;
using Oracle.ManagedDataAccess.Client;
using Akka.Persistence.Sql.Common;
using Akka.Persistence.Sql.Common.Snapshot;

namespace Akka.Persistence.Oracle.Snapshot
{
    /// <summary>
    /// Actor used for storing incoming snapshots into persistent snapshot store backed by Oracle database.
    /// </summary>
    public class OracleSnapshotStore : SqlSnapshotStore
    {
        private readonly OraclePersistence extension = OraclePersistence.Get(Context.System);

        public OracleSnapshotStore()
        {
            QueryBuilder = new OracleSnapshotQueryBuilder(extension.SnapshotSettings);
            QueryMapper = new OracleSnapshotQueryMapper(Context.System.Serialization);
        }

        protected override SnapshotStoreSettings Settings => extension.SnapshotSettings;

        protected override DbConnection CreateDbConnection(string connectionString)
        {
            return new OracleConnection(connectionString);
        }
    }
}