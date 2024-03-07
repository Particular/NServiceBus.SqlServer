namespace NServiceBus.Transport.PostgreSql
{
    using System.Collections.Generic;
    using System.Linq;
    using Routing;
    using Sql.Shared.Sending;

    class EndpointSchemaSettings
    {
        public void SpecifySchema(string endpointName, string schema)
        {
            schemas[endpointName] = schema;
        }

        public bool TryGet(string endpointName, out string schema)
        {
            return schemas.TryGetValue(endpointName, out schema);
        }

        internal List<EndpointInstance> ToEndpointInstances()
        {
            return schemas.Keys.Distinct()
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
            return result;
        }

        Dictionary<string, string> schemas = [];
    }
}