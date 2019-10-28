namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;

    class TableBasedSubscriptions : IKnowWhereTheSubscriptionsAre
    {
        SqlConnectionFactory connectionFactory;
        string subscribeCommand;
        string unsubscribeCommand;
        string getSubscribersCommand;
        string createSubscriptionsTableCommand;


        public TableBasedSubscriptions(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            // TODO: Be able to change the subscriptions table name and schema
#pragma warning disable 618
            subscribeCommand = SqlConstants.SubscribeText;
            unsubscribeCommand = SqlConstants.UnsubscribeText;
            createSubscriptionsTableCommand = SqlConstants.CreateSubscriptionTableText;
            getSubscribersCommand = SqlConstants.GetSubscribersText;
#pragma warning restore 618
        }

        public async Task Subscribe(string endpointName, string endpointAddress, string eventType)
        {
            // TODO: Do we need a specific transaction scope here?
            // TODO: Do we need to pull an existing connection out of the context here?
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using(var command = connection.CreateCommand())
            {
                command.CommandText = subscribeCommand;
                command.Parameters.Add("Subscriber", SqlDbType.VarChar).Value = endpointName;
                command.Parameters.Add("MessageType", SqlDbType.VarChar).Value = eventType;
                command.Parameters.Add("Endpoint", SqlDbType.VarChar).Value = endpointAddress;

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task Unsubscribe(string endpointName, string eventType)
        {
            // TODO: Do we need a specific transaction scope here?
            // TODO: Do we need to pull an existing connection out of the context here?
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using(var command = connection.CreateCommand())
            {
                command.CommandText = unsubscribeCommand;
                command.Parameters.Add("Subscriber", SqlDbType.VarChar).Value = endpointName;
                command.Parameters.Add("MessageType", SqlDbType.VarChar).Value = eventType;

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<string>> GetSubscribersForEvent(string eventType)
        {
            var results = new List<string>();
            using(var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = getSubscribersCommand;
                command.Parameters.Add("MessageType", SqlDbType.VarChar).Value = eventType;

                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        results.Add(reader.GetString(1));
                    }
                }
                return results;
            }
        }

        public async Task CreateSubscriptionTable()
        {
            // TODO: Call this from somewhere
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = createSubscriptionsTableCommand;

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}