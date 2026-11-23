namespace NServiceBus.Transport.Sql.Shared
{
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    class SubscriptionTable(ISqlConstants sqlConstants, string qualifiedTableName, DbConnectionFactory connectionFactory)
    {
        internal static void AddTopicParameters(IDbCommand command, string[] topics)
        {
            for (var i = 0; i < topics.Length; i++)
            {
                command.AddParameter($"Topic_{i}", DbType.String, topics[i], TopicParameterSize);
            }
        }

        // The largest fixed nvarchar size. Longer values are truncated, which cannot match a stored topic (the column holds 200 characters).
        const int TopicParameterSize = 4000;

        string subscribeCommand = string.Format(sqlConstants.SubscribeText, qualifiedTableName);
        string unsubscribeCommand = string.Format(sqlConstants.UnsubscribeText, qualifiedTableName);

        public async Task Subscribe(string endpointName, string queueAddress, string topic, CancellationToken cancellationToken = default)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
                await using var connectionDisposer = connection.ConfigureAwait(false);
                var command = connection.CreateCommand();
                await using var commandDisposer = command.ConfigureAwait(false);
                command.CommandText = subscribeCommand;

                command.AddParameter("Endpoint", DbType.String, endpointName);
                command.AddParameter("QueueAddress", DbType.String, queueAddress);
                command.AddParameter("Topic", DbType.String, topic);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task Unsubscribe(string endpointName, string topic, CancellationToken cancellationToken = default)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
                await using var connectionDisposer = connection.ConfigureAwait(false);
                var command = connection.CreateCommand();
                await using var commandDisposer = command.ConfigureAwait(false);
                command.CommandText = unsubscribeCommand;
                command.AddParameter("Endpoint", DbType.String, endpointName);
                command.AddParameter("Topic", DbType.String, topic);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<List<string>> GetSubscribers(string[] topics, CancellationToken cancellationToken = default)
        {
            var results = new List<string>();

            var argumentsList = string.Join(", ", Enumerable.Range(0, topics.Length).Select(i => $"@Topic_{i}"));
            var getSubscribersCommand = string.Format(sqlConstants.GetSubscribersText, qualifiedTableName, argumentsList);

            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
                await using var connectionDisposer = connection.ConfigureAwait(false);
                var command = connection.CreateCommand();
                await using var commandDisposer = command.ConfigureAwait(false);
                command.CommandText = getSubscribersCommand;
                AddTopicParameters(command, topics);

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
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