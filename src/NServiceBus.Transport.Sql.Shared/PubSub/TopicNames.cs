namespace NServiceBus.Transport.Sql.Shared
{
    using System;

    static class TopicName
    {
        public static string From(Type type) => type.FullName;
    }
}