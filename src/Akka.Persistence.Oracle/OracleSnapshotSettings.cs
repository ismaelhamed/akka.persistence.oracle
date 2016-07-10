using Akka.Configuration;
using Akka.Persistence.Sql.Common;

namespace Akka.Persistence.Oracle
{
    public class OracleSnapshotSettings : SnapshotStoreSettings
    {
        public const string ConfigPath = "akka.persistence.snapshot-store.oracle";

        /// <summary>
        /// Flag determining in in case of snapshot store table missing, it should be automatically initialized.
        /// </summary>
        public bool AutoInitialize
        {
            get; 
            private set;
        }

        public OracleSnapshotSettings(Config config) : base(config)
        {
            AutoInitialize = config.GetBoolean("auto-initialize");
        }
    }
}