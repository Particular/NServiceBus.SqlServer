namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;

    interface ITopicManager
    {
        IEnumerable<string> GetTopicsFor(Type messageType);
    }
}