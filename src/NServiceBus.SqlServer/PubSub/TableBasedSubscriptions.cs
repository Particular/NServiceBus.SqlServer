namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;
    using System.Transactions;

    class TableBasedSubscriptions : IManageTransportSubscriptions
    {
        SqlConnectionFactory connectionFactory;
        string subscribeCommand;
        string unsubscribeCommand;
        string getSubscribersCommand;

        public TableBasedSubscriptions(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            // TODO: Be able to change the subscriptions table name and schema
#pragma warning disable 618
            subscribeCommand = SqlConstants.SubscribeText;
            unsubscribeCommand = SqlConstants.UnsubscribeText;
            getSubscribersCommand = SqlConstants.GetSubscribersText;
#pragma warning restore 618
        }

        public async Task Subscribe(string endpointName, string endpointAddress, Type eventType)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = subscribeCommand;
                    command.Parameters.Add("Subscriber", SqlDbType.VarChar).Value = endpointName;
                    command.Parameters.Add("MessageType", SqlDbType.VarChar).Value = eventType.ToString();
                    command.Parameters.Add("Endpoint", SqlDbType.VarChar).Value = endpointAddress;

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task Unsubscribe(string endpointName, Type eventType)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = unsubscribeCommand;
                    command.Parameters.Add("Subscriber", SqlDbType.VarChar).Value = endpointName;
                    command.Parameters.Add("MessageType", SqlDbType.VarChar).Value = eventType.ToString();

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<List<string>> GetSubscribersForEvent(Type eventType)
        {
            var results = new List<string>();
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = getSubscribersCommand;
                    command.Parameters.Add("MessageType", SqlDbType.VarChar).Value = eventType.ToString();

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
        }
    }
}