using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Common;

namespace Akka.Persistence.Oracle
{
    /// <inheritdoc />
    /// <summary>
    /// Configuration settings representation targeting Oracle journal actor.
    /// </summary>
    public class OracleJournalSettings : JournalSettings
    {
        public static readonly string ConfigPath = "akka.persistence.journal.oracle";

        public OracleJournalSettings(Config config)
            : base(config)
        { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Configuration settings representation targeting Oracle snapshot store actor.
    /// </summary>
    public class OracleSnapshotSettings : SnapshotStoreSettings
    {
        public static readonly string ConfigPath = "akka.persistence.snapshot-store.oracle";

        public OracleSnapshotSettings(Config config) 
            : base(config)
        { }
    }

    /// <inheritdoc />
    /// <summary>
    /// An actor system extension initializing support for Oracle persistence layer.
    /// </summary>
    public class OraclePersistence : IExtension
    {
        /// <summary>
        /// Journal-related settings loaded from HOCON configuration.
        /// </summary>
        public readonly Config DefaultJournalConfig;

        /// <summary>
        /// Snapshot store related settings loaded from HOCON configuration.
        /// </summary>
        public readonly Config DefaultSnapshotConfig;

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
            var defaultConfig = DefaultConfiguration();
            system.Settings.InjectTopLevelFallback(defaultConfig);

            DefaultJournalConfig = defaultConfig.GetConfig(OracleJournalSettings.ConfigPath);
            DefaultSnapshotConfig = defaultConfig.GetConfig(OracleSnapshotSettings.ConfigPath);
        }
    }

    /// <summary>
    /// Singleton class used to setup Oracle backend for akka persistence plugin.
    /// </summary>
    public class OraclePersistenceProvider : ExtensionIdProvider<OraclePersistence>
    {
        /// <summary>
        /// Creates an actor system extension for akka persistence SQL Server support.
        /// </summary>
        public override OraclePersistence CreateExtension(ExtendedActorSystem system)
        {
            return new OraclePersistence(system);
        }
    }
}
