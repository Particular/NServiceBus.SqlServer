namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class QueueSchemaAndCatalogSettings
    {
        public void SpecifySchema(string queueName, string schema)
        {
            schemas[queueName] = schema;
        }

        public void SpecifyCatalog(string queueName, string catalog)
        {
            catalogs[queueName] = catalog;
        }

        public void SpecifyInstance(string queueName, string instance)
        {
            instances[queueName] = instance;
        }

        public void TryGet(string queueName, out string schema, out string catalog, out string instance)
        {
            schemas.TryGetValue(queueName, out schema);
            catalogs.TryGetValue(queueName, out catalog);
            instances.TryGetValue(queueName, out instance);
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
        Dictionary<string, string> catalogs = new Dictionary<string, string>();
        Dictionary<string, string> instances = new Dictionary<string, string>();
    }
}