namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Linq;
    using Routing;

    class EndpointSchemasSettings
    {
        public void AddOrUpdate(string endpointName, string schema)
        {
            if (schemas.ContainsKey(endpointName) == false)
            {
                schemas.Add(endpointName, schema);
            }
            else
            {
                schemas[endpointName] = schema;
            }
        }

        public bool TryGet(string endpointName, out string schema)
        {
            if (schemas.ContainsKey(endpointName))
            {
                schema = schemas[endpointName];
                return true;
            }

            schema = null;
            return false;
        }

        public List<EndpointInstance> ToEndpointInstances()
        {
            return schemas
                .Select(kv => new EndpointInstance(
                    kv.Key,
                    null,
                    new Dictionary<string, string> { { SettingsKeys.SchemaPropertyKey, kv.Value }}))
                .ToList();
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
    }
}