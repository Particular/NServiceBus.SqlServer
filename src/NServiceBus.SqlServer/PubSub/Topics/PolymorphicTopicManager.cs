namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unicast.Messages;

    class PolymorphicTopicManager : ITopicManager
    {
        MessageMetadataRegistry messageMetadataRegistry;

        public PolymorphicTopicManager(MessageMetadataRegistry messageMetadataRegistry)
        {
            this.messageMetadataRegistry = messageMetadataRegistry;
        }

        public IEnumerable<string> GetTopicsFor(Type messageType)
        {
            var messageMetadata = messageMetadataRegistry.GetMessageMetadata(messageType);
            return messageMetadata.MessageHierarchy.Select(TopicName.From);
        }
    }
}