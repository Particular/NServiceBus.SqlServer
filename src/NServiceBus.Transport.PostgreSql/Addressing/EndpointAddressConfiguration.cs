namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Routing;
    using Transport.PostgreSql;

    /// <summary>
    /// Configuration extensions for endpoint schema settings
    /// </summary>
    public static class EndpointAddressConfiguration
    {
        /// <summary>
        /// Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="settings"><see cref="RoutingSettings"/></param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        public static void UseSchemaForEndpoint(this RoutingSettings settings, string endpointName, string schema)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

            var localEndpointName = settings.GetSettings().EndpointName();

            if (endpointName.Equals(localEndpointName))
            {
                throw new ArgumentException("Custom schema cannot be specified for the local endpoint.");
            }

            var schemaAndCatalogSettings = settings.GetSettings().GetOrCreate<EndpointSchemaSettings>();

            schemaAndCatalogSettings.SpecifySchema(endpointName, schema);

            settings.GetSettings().GetOrCreate<EndpointInstances>()
                .AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());
        }
    }
}