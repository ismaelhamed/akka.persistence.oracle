using Akka.Configuration;
using Akka.Persistence.Sql.Common;

namespace Akka.Persistence.Oracle
{
    public class OracleJournalSettings : JournalSettings
    {
        public const string ConfigPath = "akka.persistence.journal.oracle";

        /// <summary>
        /// Flag determining in in case of event journal table missing, it should be automatically initialized.
        /// </summary>
        public bool AutoInitialize
        {
            get; 
            private set;
        }

        /// <summary>
        /// Metadata table name
        /// </summary>
        public string MetadataTableName
        {
            get; 
            private set;
        }

        public OracleJournalSettings(Config config) 
            : base(config)
        {
            AutoInitialize = config.GetBoolean("auto-initialize");
            MetadataTableName = config.GetString("metadata-table-name");
        }
    }
}