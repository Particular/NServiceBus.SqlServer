namespace SqlServerV1
{
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using TransportCompatibilityTests.Common.SqlServer;

    class CustomConfiguration : IConfigurationSource
    {
        MessageMapping[] messageMappings;

        public CustomConfiguration(MessageMapping[] messageMappings)
        {
            this.messageMappings = messageMappings;
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

                messageMappings
                    .Select(
                        mm =>
                            new MessageEndpointMapping
                            {
                                Messages = mm.MessageType.AssemblyQualifiedName,
                                Endpoint = mm.TransportAddress
                            })
                    .ToList()
                    .ForEach(em => endpointMappingsCollection.Add(em));

                return new UnicastBusConfig
                {
                    MessageEndpointMappings = endpointMappingsCollection
                } as T;
            }

            // leaving the rest of the configuration as is:
            return ConfigurationManager.GetSection(typeof(T).Name) as T;
        }
    }
}