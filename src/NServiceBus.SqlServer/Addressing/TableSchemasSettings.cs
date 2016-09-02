namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class TableSchemasSettings
    {
        public void AddOrUpdate(string tableName, string schema)
        {
            if (schemas.ContainsKey(tableName) == false)
            {
                schemas.Add(tableName, schema);
            }
            else
            {
                schemas[tableName] = schema;
            }
        }

        public bool TryGet(string tableName, out string schema)
        {
            if (schemas.ContainsKey(tableName))
            {
                schema = schemas[tableName];
                return true;
            }

            schema = null;
            return false;
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
    }
}