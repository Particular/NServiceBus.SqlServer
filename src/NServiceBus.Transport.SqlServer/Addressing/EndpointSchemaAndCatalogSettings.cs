namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;
    using System.Linq;
    using Routing;

    class EndpointSchemaAndCatalogSettings
    {
        public void SpecifySchema(string endpointName, string schema)
        {
            schemas[endpointName] = schema;
        }

        public void SpecifyCatalog(string endpointName, string catalog)
        {
            catalogs[endpointName] = catalog;
        }

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