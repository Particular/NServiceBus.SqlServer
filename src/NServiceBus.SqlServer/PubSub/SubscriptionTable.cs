namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
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
#pragma warning disable 618
            subscribeCommand = string.Format(SqlConstants.SubscribeText, qualifiedTableName);
            unsubscribeCommand = string.Format(SqlConstants.UnsubscribeText, qualifiedTableName);
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
                    
                    var endpointParameter = command.CreateParameter();
                    endpointParameter.ParameterName = "Endpoint";
                    endpointParameter.DbType = DbType.AnsiString;
                    endpointParameter.Value = endpointName;
                    command.Parameters.Add(endpointParameter);
                    
                    var queueAddressParameter = command.CreateParameter();
                    queueAddressParameter.ParameterName = "QueueAddress";
                    queueAddressParameter.DbType = DbType.AnsiString;
                    queueAddressParameter.Value = queueAddress;
                    command.Parameters.Add(queueAddressParameter);
                    
                    var topicParameter = command.CreateParameter();
                    topicParameter.ParameterName = "Topic";
                    topicParameter.DbType = DbType.AnsiString;
                    topicParameter.Value = topic;
                    command.Parameters.Add(topicParameter);
                    
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
                    
                    var endpointParameter = command.CreateParameter();
                    endpointParameter.ParameterName = "Endpoint";
                    endpointParameter.DbType = DbType.AnsiString;
                    endpointParameter.Value = endpointName;
                    command.Parameters.Add(endpointParameter);
                    
                    var topicParameter = command.CreateParameter();
                    topicParameter.ParameterName = "Topic";
                    topicParameter.DbType = DbType.AnsiString;
                    topicParameter.Value = topic;
                    command.Parameters.Add(topicParameter);
                    
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<List<string>> GetSubscribers(string[] topics)
        {
            var results = new List<string>();

            var argumentsList = string.Join(", ", Enumerable.Range(0, topics.Length).Select(i => $"@Topic_{i}"));
#pragma warning disable 618
            var getSubscribersCommand = string.Format(SqlConstants.GetSubscribersText, qualifiedTableName, argumentsList);
#pragma warning restore 618

            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = getSubscribersCommand;
                    for (var i = 0; i < topics.Length; i++)
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = $"Topic_{i}";
                        parameter.DbType = DbType.AnsiString;
                        parameter.Value = topics[i];
                        command.Parameters.Add(parameter);
                    }

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