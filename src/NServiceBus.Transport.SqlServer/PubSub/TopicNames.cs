namespace NServiceBus.Transport.SqlServer
{
    using System;

    static class TopicName
    {
        public static string From(Type type) => type.FullName;
    }
}