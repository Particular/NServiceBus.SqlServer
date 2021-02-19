namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    class SubscriptionTable
    {
        string qualifiedTableName;
        SqlConnectionFactory connectionFactory;
        string subscribeCommand;
        string unsubscribeCommand;

        public SubscriptionTable(string qualifiedTableName, SqlConnectionFactory connectionFactory)
        {
            this.qualifiedTableName = qualifiedTableName;
            this.connectionFactory = connectionFactory;
            subscribeCommand = string.Format(SqlConstants.SubscribeText, qualifiedTableName);
            unsubscribeCommand = string.Format(SqlConstants.UnsubscribeText, qualifiedTableName);
        }

        public async Task Subscribe(string endpointName, string queueAddress, string topic, CancellationToken cancellationToken = default)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = subscribeCommand;
                    command.Parameters.Add("Endpoint", SqlDbType.VarChar).Value = endpointName;
                    command.Parameters.Add("QueueAddress", SqlDbType.VarChar).Value = queueAddress;
                    command.Parameters.Add("Topic", SqlDbType.VarChar).Value = topic;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task Unsubscribe(string endpointName, string topic, CancellationToken cancellationToken = default)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = unsubscribeCommand;
                    command.Parameters.Add("Endpoint", SqlDbType.VarChar).Value = endpointName;
                    command.Parameters.Add("Topic", SqlDbType.VarChar).Value = topic;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task<List<string>> GetSubscribers(string[] topics, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();

            var argumentsList = string.Join(", ", Enumerable.Range(0, topics.Length).Select(i => $"@Topic_{i}"));
            var getSubscribersCommand = string.Format(SqlConstants.GetSubscribersText, qualifiedTableName, argumentsList);

            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = getSubscribersCommand;
                    for (var i = 0; i < topics.Length; i++)
                    {
                        command.Parameters.Add($"Topic_{i}", SqlDbType.VarChar).Value = topics[i];
                    }

                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
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