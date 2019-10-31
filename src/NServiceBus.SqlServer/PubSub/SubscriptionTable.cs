namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;
    using System.Transactions;

    class SubscriptionTable : ISubscriptionStore
    {
        SqlConnectionFactory connectionFactory;
        string subscribeCommand;
        string unsubscribeCommand;
        string getSubscribersCommand;

        public SubscriptionTable(string qualifiedTableName, SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            // TODO: Be able to change the subscriptions table name and schema
#pragma warning disable 618
            subscribeCommand = string.Format(SqlConstants.SubscribeText, qualifiedTableName);
            unsubscribeCommand = string.Format(SqlConstants.UnsubscribeText, qualifiedTableName);
            getSubscribersCommand = string.Format(SqlConstants.GetSubscribersText, qualifiedTableName);
#pragma warning restore 618
        }

        public async Task Subscribe(string endpointName, string queueAddress, string topic)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = subscribeCommand;
                    command.Parameters.Add("Endpoint", SqlDbType.VarChar).Value = endpointName;
                    command.Parameters.Add("QueueAddress", SqlDbType.VarChar).Value = queueAddress;
                    command.Parameters.Add("Topic", SqlDbType.VarChar).Value = topic;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task Unsubscribe(string endpointName, string topic)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = unsubscribeCommand;
                    command.Parameters.Add("Endpoint", SqlDbType.VarChar).Value = endpointName;
                    command.Parameters.Add("Topic", SqlDbType.VarChar).Value = topic;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<List<string>> GetSubscribersForTopic(string topic)
        {
            var results = new List<string>();
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = getSubscribersCommand;
                    command.Parameters.Add("Topic", SqlDbType.VarChar).Value = topic;

                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            results.Add(reader.GetString(0));
                        }
                    }

                    return results;
                }
            }
        }
    }
}