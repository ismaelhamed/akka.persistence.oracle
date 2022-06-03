//-----------------------------------------------------------------------
// <copyright file="DbUtils.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.IO;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle.Tests
{
    public static class DbUtils
    {
        public static string ConnectionString { get; private set; }

        public static void Initialize()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddXmlFile("AppConfig.xml").Build();

            ConnectionString = config.GetSection("connectionStrings:add:TestDb")["connectionString"];

            // Delete local snapshot flat file database
            var path = "./snapshots";
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        public static void Clean()
        {
            var connectionBuilder = new OracleConnectionStringBuilder(ConnectionString);

            using var conn = new OracleConnection(ConnectionString);
            conn.Open();

            DropTables(conn, connectionBuilder.UserID);

            // Delete local snapshot flat file database
            var path = "./snapshots";
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        private static void DropTables(OracleConnection conn, string schemaName)
        {
            using var cmd = new OracleCommand();
            cmd.CommandText = string.Format(@"
BEGIN
  BEGIN EXECUTE IMMEDIATE 'DROP TABLE {0}.EVENTJOURNAL'; EXCEPTION WHEN OTHERS THEN NULL; END;  
  BEGIN EXECUTE IMMEDIATE 'DROP TABLE {0}.SNAPSHOTSTORE'; EXCEPTION WHEN OTHERS THEN NULL; END;
  BEGIN EXECUTE IMMEDIATE 'DROP TABLE {0}.METADATA'; EXCEPTION WHEN OTHERS THEN NULL; END;
  BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE {0}.EVENTJOURNAL_SEQ'; EXCEPTION WHEN OTHERS THEN NULL; END;
END;", schemaName);

            cmd.Connection = conn;
            cmd.ExecuteNonQuery();
        }
    }
}
