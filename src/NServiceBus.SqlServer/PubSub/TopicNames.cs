namespace NServiceBus.Transport.SQLServer
{
    using System;

    static class TopicName
    {
        public static string From(Type type) => type.FullName;
    }
}