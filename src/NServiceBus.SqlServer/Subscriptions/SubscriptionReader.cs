namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;

    class SubscriptionReader
    {
        string subscriptionSchema;
        string subscriptionsTable;
        SqlConnectionFactory connectionFactory;
        IReadOnlyCollection<Type> allMessageTypes;

        public SubscriptionReader(string subscriptionSchema, string subscriptionsTable, SqlConnectionFactory connectionFactory, IReadOnlyCollection<Type> allMessageTypes)
        {
            this.subscriptionSchema = subscriptionSchema;
            this.subscriptionsTable = subscriptionsTable;
            this.connectionFactory = connectionFactory;
            this.allMessageTypes = allMessageTypes;
        }

        public async Task<IEnumerable<Subscriber>> GetSubscribersFor(Type messageType)
        {
            var typesEnclosed = allMessageTypes.Where(t => t.IsAssignableFrom(messageType));
            using (var conn = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var results = new List<Subscriber>();
                var typeParams = typesEnclosed.Select((t, i) => new { TypeName = t.FullName, ParamName = $"@Type{i}" }).ToArray();
                var typeParamsDeclaration = string.Join(", ", typeParams.Select(p => p.ParamName));
                using (var cmd = new SqlCommand($@"SELECT Endpoint, TransportAddress FROM [{subscriptionSchema}].[{subscriptionsTable}] WHERE TypeName in ({typeParamsDeclaration}) GROUP BY Endpoint, TransportAddress", conn))
                {
                    foreach (var typeParam in typeParams)
                    {
                        cmd.Parameters.Add(typeParam.ParamName, SqlDbType.VarChar).Value = typeParam.TypeName;
                    }
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (reader.Read())
                        {
                            var endpoint = reader.GetString(0);
                            var transportAddress = reader.GetString(1);
                            var subscription = new Subscriber(endpoint, transportAddress);
                            results.Add(subscription);
                        }
                    }
                }
                return results;
            }
        }

    }
}