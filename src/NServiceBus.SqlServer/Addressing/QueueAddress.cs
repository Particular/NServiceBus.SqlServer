namespace NServiceBus.Transport.SQLServer
{
    using System.Data.SqlClient;

    class QueueAddress
    {
        public QueueAddress(string tableName, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);

            TableName = UnescapeIdentifier(tableName);
            SchemaName = UnescapeIdentifier(schemaName);
        }

        public string TableName { get; }
        public string SchemaName { get; }

        public static QueueAddress Parse(string address)
        {
            if (address.Contains("@"))
            {
                var parts = address.Split('@');
                var tableName = parts[0];
                var schemaName = parts[1];

                return new QueueAddress(tableName, schemaName);
            }

            return new QueueAddress(address, null);
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(SchemaName))
            {
                return $"{TableName}@[{SchemaName}]";
            }

            return TableName;
        }

        static string UnescapeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }

            using (var sanitizer = new SqlCommandBuilder())
            {
                return sanitizer.UnquoteIdentifier(identifier);
            }
        }

        bool Equals(QueueAddress other)
        {
            return string.Equals(TableName, other.TableName) && string.Equals(SchemaName, other.SchemaName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((QueueAddress) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((TableName != null ? TableName.GetHashCode() : 0)*397) ^ (SchemaName != null ? SchemaName.GetHashCode() : 0);
            }
        }
    }
}