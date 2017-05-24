using System.Collections.Generic;
using System.Configuration;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;

class CustomConfiguration : IConfigurationSource
{
    List<MessageEndpointMapping> messageMappings = new List<MessageEndpointMapping>();

    public void AddMapping(MessageEndpointMapping mapping)
    {
        messageMappings.Add(mapping);
    }

    public T GetConfiguration<T>() where T : class, new()
    {
        if (typeof(T) == typeof(MessageForwardingInCaseOfFaultConfig))
        {
            return new MessageForwardingInCaseOfFaultConfig
            {
                ErrorQueue = "error"
            } as T;
        }

        if (typeof(T) == typeof(UnicastBusConfig))
        {
            var endpointMappingsCollection = new MessageEndpointMappingCollection();

            foreach (var em in messageMappings)
            {
                endpointMappingsCollection.Add(em);
            }

            return new UnicastBusConfig
            {
                MessageEndpointMappings = endpointMappingsCollection,
            } as T;
        }

        if (typeof(T) == typeof(TransportConfig))
        {
            return new TransportConfig
            {
                MaxRetries = 0,
            } as T;
        }

        if (typeof(T) == typeof(AuditConfig))
        {
            return new AuditConfig
            {
                QueueName = "audit"
            }
            as T;
        }

        if (typeof(T) == typeof(SecondLevelRetriesConfig))
        {
            return new SecondLevelRetriesConfig
            {
                NumberOfRetries = 0
            } as T;
        }

        // leaving the rest of the configuration as is:
        return ConfigurationManager.GetSection(typeof(T).Name) as T;
    }
}
