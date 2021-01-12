namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;
    using System.Linq;
    using Routing;

    /// <summary>
    /// Catalog and schema configuration for NServiceBus endpoints.
    /// </summary>
    public class EndpointSchemaAndCatalogSettings
    {
        /// <summary>
        /// Enables specifying schema for a given endpoint.
        /// </summary>
        public void SpecifySchema(string endpointName, string schema)
        {
            schemas[endpointName] = schema;
        }

        /// <summary>
        /// Enables specifying catalog for a given endpoint.
        /// </summary>
        public void SpecifyCatalog(string endpointName, string catalog)
        {
            catalogs[endpointName] = catalog;
        }

        /// <summary>
        /// Returns schema configuration for a given endpoint.
        /// </summary>
        public bool TryGet(string endpointName, out string schema)
        {
            return schemas.TryGetValue(endpointName, out schema);
        }

        internal List<EndpointInstance> ToEndpointInstances()
        {
            return schemas.Keys.Concat(catalogs.Keys).Distinct()
                .Select(endpoint => new EndpointInstance(endpoint, null, GetProperties(endpoint)))
                .ToList();
        }

        Dictionary<string, string> GetProperties(string endpoint)
        {
            var result = new Dictionary<string, string>();
            if (schemas.TryGetValue(endpoint, out var schema))
            {
                result[SettingsKeys.SchemaPropertyKey] = schema;
            }
            if (catalogs.TryGetValue(endpoint, out var catalog))
            {
                result[SettingsKeys.CatalogPropertyKey] = catalog;
            }
            return result;
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
        Dictionary<string, string> catalogs = new Dictionary<string, string>();
    }
}