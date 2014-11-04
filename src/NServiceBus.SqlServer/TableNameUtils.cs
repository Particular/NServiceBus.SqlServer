namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    static class TableNameUtils
    {
        public static string GetTableName(this Address address, bool schemaAwareAddressing)
        {
            if (!schemaAwareAddressing)
            {
                return GetTableName(address.Queue, null);
            }
            var parts = address.Queue.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("When schema-aware addressing is enabled, addresses in message-endpoint mappings must include schema name e.g. dbo.SomeEndpoint.");
            }
            var queueName = address.Queue.Substring(address.Queue.IndexOf(".", StringComparison.Ordinal) + 1);
            var schemaName = parts[0];
            return GetTableName(queueName, schemaName);
        }

        //public static string GetTableName(this Address address, string schemaName)
        //{
        //    return GetTableName(address.Queue, schemaName);
        //}
        
        public static string GetTableName(this string queueName, string schemaName)
        {
            var tableName = queueName.Length > 128 
                ? DeterministicGuidBuilder(queueName).ToString() 
                : queueName;

            return schemaName != null
                ? "[" + schemaName + "].[" + tableName + "]"
                : "[" + tableName + "]";
        }

        private static Guid DeterministicGuidBuilder(string input)
        {
            // use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                return new Guid(hashBytes);
            }
        }
    }
}