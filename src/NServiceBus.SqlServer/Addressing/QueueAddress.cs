namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    class QueueAddress
    {
        public QueueAddress(string catalog, string schemaName, string tableName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);
            if (catalog != null && IsEmptyOrWhiteSpace(catalog))
            {
                throw new ArgumentException("Catalog name cannot be empty or whitespace.", nameof(catalog));
            }
            if (catalog != null && schemaName == null)
            {
                throw new Exception("If catalog is specified, schema name has to be specified too.");
            }
            Catalog = Unquote(catalog);
            TableName = tableName;
            SchemaName = Unquote(schemaName);
            Quoted = string.Join(".", GetAllParts().Select(QuoteIdentifier));
            Unquoted = string.Join(".", GetAllParts());
        }

        static bool IsEmptyOrWhiteSpace(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }
            return true;
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
            var location = address;

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
                else if (location[i] == '@' && (rightBrackets % 2 != 0 || (rightBrackets == 0 && leftBrackets == 0)))
                {
                    var part = location.Substring(startIndex, i - startIndex);
                    locationParts.Add(part);
                    startIndex = i + 1;
                    rightBrackets = 0;
                    leftBrackets = 0;
                }
            }
            locationParts.Add(location.Substring(startIndex));

            if (locationParts.Count > 3)
            {
                throw new Exception("Invalid address. Expect address in format Table@[Schema]@[Catalog]");
            }

            var tableName = locationParts[0];
            var schemaName = locationParts.Count > 1 ? locationParts[1] : null;
            var catalogName = locationParts.Count > 2 ? locationParts[2] : null;

            return new QueueAddress(catalogName, schemaName, tableName);
        }

        public override string ToString()
        {
            var result = string.Join("@", new[]
            {
                TableName,
                Quote(SchemaName),
                Quote(Catalog)
            }.Where(x => x != null));
            return result;
        }

        static string Unquote(string name)
        {
            return name == null
                ? null
                : UnquoteIdentifier(name);
        }

        static string Quote(string name)
        {
            return name == null
                ? null
                : QuoteIdentifier(name);
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