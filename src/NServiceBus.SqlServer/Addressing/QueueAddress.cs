namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    class QueueAddress
    {
        public QueueAddress(string tableName, string schemaName)
            : this(null, schemaName, tableName)
        {
        }

        public QueueAddress(string catalog, string schemaName, string tableName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);
            if (catalog == "")
            {
                throw new ArgumentException("Catalog name cannot be empty.", nameof(catalog));
            }
            if (schemaName == "")
            {
                throw new ArgumentException("Schema name cannot be empty.", nameof(schemaName));
            }
            if (catalog != null && schemaName == null)
            {
                throw new Exception("If catalog is specified, schema name has to be specified too.");
            }
            Catalog = Unquaote(catalog);
            TableName = tableName;
            SchemaName = Unquaote(schemaName);
            Quoted = string.Join(".", GetAllParts().Select(QuoteIdentifier));
            Unquoted = string.Join(".", GetAllParts());
        }

        IEnumerable<string> GetAllParts()
        {
            return new[]
            {
                Catalog,
                SchemaName,
                TableName
            }.Where(x => x != null);
        }

        public QueueAddress OverrideSchema(string schemaName)
        {
            if (HasDefinedSchema)
            {
                throw new Exception("Cannot override a schema if it is defined in an address.");
            }
            return new QueueAddress(Catalog, schemaName, TableName);
        }

        public string Catalog { get; }
        public string TableName { get; }
        public string SchemaName { get; }
        public string Unquoted { get; }
        public string Quoted { get; }
        public bool HasDefinedSchema => !string.IsNullOrWhiteSpace(SchemaName);

        public static QueueAddress Parse(string address)
        {
            if (address.Contains("@"))
            {
                var parts = address.Split('@');
                var tableName = parts[0];
                var location = parts[1];

                var rightBrackets = 0;
                var leftBrackets = 0;
                var startIndex = 0;
                var locationParts = new List<string>();
                for (var i = 0; i < location.Length; i++)
                {
                    if (location[i] == ']')
                    {
                        rightBrackets++;
                    }
                    else if (location[i] == '[')
                    {
                        leftBrackets++;
                    }
                    else if (location[i] == '.' && (rightBrackets % 2 != 0 || (rightBrackets == 0 && leftBrackets == 0)))
                    {
                        var part = location.Substring(startIndex, i - startIndex);
                        locationParts.Add(part);
                        startIndex = i + 1;
                        rightBrackets = 0;
                        leftBrackets = 0;
                    }
                }
                locationParts.Add(location.Substring(startIndex));

                if (locationParts.Count == 1)
                {
                    return new QueueAddress(null, locationParts[0], tableName);
                }
                if (locationParts.Count == 2)
                {
                    return new QueueAddress(locationParts[0], locationParts[1], tableName);
                }
                throw new Exception("Invalid address. Expect address in format Table@[Catalog].[Schema]");
            }

            return new QueueAddress(null, null, address);
        }

        public override string ToString()
        {
            if (SchemaName == null)
            {
                return TableName;
            }
            var location = string.Join(".", new[]
            {
                Catalog,
                SchemaName
            }.Where(x => x != null).Select(QuoteIdentifier));
            return $"{TableName}@{location}";
        }

        static string Unquaote(string name)
        {
            return name == null
                ? null
                : UnquoteIdentifier(name);
        }

        static string UnquoteIdentifier(string name)
        {
            using (var sanitizer = new SqlCommandBuilder())
            {
                return sanitizer.UnquoteIdentifier(name);
            }
        }

        static string QuoteIdentifier(string name)
        {
            using (var sanitizer = new SqlCommandBuilder())
            {
                return sanitizer.QuoteIdentifier(name);
            }
        }
    }
}