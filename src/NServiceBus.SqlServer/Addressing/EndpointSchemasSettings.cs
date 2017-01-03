namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;
    using Routing;

    class SchemaAndCatalogSettings
    {
        public void SpecifySchema(string endpointName, string schema)
        {
            schemas[endpointName] = schema;
        }

        public void SpecifyCatalog(string endpointName, string catalog)
        {
            catalogs[endpointName] = catalog;
        }

        public void SpecifyRemoteCatalog(string catalog, QueueAddress outgoingQueue)
        {
            remoteCatalogs[catalog] = outgoingQueue;
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

        public QueueAddress GetImmediateAddress(QueueAddress ultimateAddress, Dictionary<string, string> headers)
        {
            QueueAddress remoteCatalogOutgoingQueue;
            if (ultimateAddress.Catalog != null && remoteCatalogs.TryGetValue(ultimateAddress.Catalog, out remoteCatalogOutgoingQueue))
            {
                headers["NServiceBus.SqlServer.Destination"] = ultimateAddress.ToString();
                return remoteCatalogOutgoingQueue;
            }
            return ultimateAddress;
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
        Dictionary<string, QueueAddress> remoteCatalogs = new Dictionary<string, QueueAddress>();
    }
}