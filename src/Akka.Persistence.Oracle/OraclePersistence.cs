using System.Configuration;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Persistence.Oracle
{
    /// <summary>
    /// An actor system extension initializing support for Oracle persistence layer.
    /// </summary>
    public class OraclePersistence : IExtension
    {
        /// <summary>
        /// Journal-related settings loaded from HOCON configuration.
        /// </summary>
        public readonly OracleJournalSettings JournalSettings;

        /// <summary>
        /// Snapshot store related settings loaded from HOCON configuration.
        /// </summary>
        public readonly OracleSnapshotSettings SnapshotSettings;

        /// <summary>
        /// Returns a default configuration for akka persistence Oracle-based journals and snapshot stores.
        /// </summary>
        /// <returns></returns>
        public static Config DefaultConfiguration()
        {
            return ConfigurationFactory.FromResource<OraclePersistence>("Akka.Persistence.Oracle.oracle.conf");
        }

        public static OraclePersistence Get(ActorSystem system)
        {
            return system.WithExtension<OraclePersistence, OraclePersistenceProvider>();
        }

        public OraclePersistence(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());

            JournalSettings = new OracleJournalSettings(system.Settings.Config.GetConfig(OracleJournalSettings.ConfigPath));
            SnapshotSettings = new OracleSnapshotSettings(system.Settings.Config.GetConfig(OracleSnapshotSettings.ConfigPath));

            if (JournalSettings.AutoInitialize)
            {
                var connectionString = string.IsNullOrEmpty(JournalSettings.ConnectionString)
                    ? ConfigurationManager.ConnectionStrings[JournalSettings.ConnectionStringName].ConnectionString
                    : JournalSettings.ConnectionString;

                OracleInitializer.CreateOracleJournalTables(connectionString, JournalSettings.SchemaName, JournalSettings.TableName);
                OracleInitializer.CreateOracleMetadataTables(connectionString, JournalSettings.SchemaName, JournalSettings.MetadataTableName);
            }

            if (SnapshotSettings.AutoInitialize)
            {
                var connectionString = string.IsNullOrEmpty(SnapshotSettings.ConnectionString)
                    ? ConfigurationManager.ConnectionStrings[SnapshotSettings.ConnectionStringName].ConnectionString
                    : SnapshotSettings.ConnectionString;

                OracleInitializer.CreateOracleSnapshotStoreTables(connectionString, SnapshotSettings.SchemaName, SnapshotSettings.TableName);
            }
        }
    }
}