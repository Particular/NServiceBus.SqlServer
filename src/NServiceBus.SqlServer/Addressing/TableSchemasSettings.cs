namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class TableSchemasSettings
    {
        public void AddOrUpdate(string tableName, string schema)
        {
            schemas[tableName] = schema;
        }

        public bool TryGet(string tableName, out string schema)
        {
            return schemas.TryGetValue(tableName, out schema);
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
    }
}