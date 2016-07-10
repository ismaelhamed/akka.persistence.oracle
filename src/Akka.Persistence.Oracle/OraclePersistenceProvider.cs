using Akka.Actor;

namespace Akka.Persistence.Oracle
{
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