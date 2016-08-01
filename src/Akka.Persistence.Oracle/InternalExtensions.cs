using System;

namespace Akka.Persistence.Oracle
{
    internal static class InternalExtensions
    {
        public static string QualifiedTypeName(this Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }
    }
}