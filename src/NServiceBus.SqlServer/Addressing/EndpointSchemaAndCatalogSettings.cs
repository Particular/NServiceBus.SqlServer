namespace NServiceBus.Transport.SQLServer
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

        public List<EndpointInstance> ToEndpointInstances()
        {
            return schemas.Keys.Concat(catalogs.Keys).Distinct()
                .Select(endpoint => new EndpointInstance(endpoint, null, GetProperties(endpoint)))
                .ToList();
        }

        Dictionary<string, string> GetProperties(string endpoint)
        {
            var result = new Dictionary<string, string>();
            string schema, catalog;
            if (schemas.TryGetValue(endpoint, out schema))
            {
                result[SettingsKeys.SchemaPropertyKey] = schema;
            }
            if (catalogs.TryGetValue(endpoint, out catalog))
            {
                result[SettingsKeys.CatalogPropertyKey] = catalog;
            }
            return result;
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
        Dictionary<string, string> catalogs = new Dictionary<string, string>();
    }
}