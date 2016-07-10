using System;
using Oracle.ManagedDataAccess.Client;

namespace Akka.Persistence.Oracle
{
    internal static class OracleCommandBuilderExtensions
    {
        public static string SafeUnquoteIdentifier(this OracleCommandBuilder target, string quotedIdentifier)
        {
            if (string.IsNullOrEmpty(quotedIdentifier))
            {
                throw new ArgumentNullException(nameof(quotedIdentifier), "Quoted identifier parameter cannot be null");
            }

            var unquotedIdentifier = quotedIdentifier.Trim();
            if (unquotedIdentifier.StartsWith(target.QuotePrefix))
            {
                unquotedIdentifier = unquotedIdentifier.Remove(0, 1);
            }

            if (unquotedIdentifier.EndsWith(target.QuoteSuffix))
            {
                unquotedIdentifier = unquotedIdentifier.Remove(unquotedIdentifier.Length - 1, 1);
            }

            return unquotedIdentifier;
        }
    }
}