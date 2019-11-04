namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;

    class BasicTopicManager : ITopicManager
    {
        public IEnumerable<string> GetTopicsFor(Type messageType)
        {
            yield return TopicName.From(messageType);
        }
    }
}