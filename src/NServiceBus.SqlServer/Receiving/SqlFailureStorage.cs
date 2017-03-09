namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using System.Transactions;

    class SqlFailureStorage
    {
        public async Task RecordFailureInfoForMessage(SqlConnectionFactory connectionFactory, string messageId, Exception exception)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var commandText = Sql.UpdateFailureStorage;

                using (var command = new SqlCommand(commandText, connection, null))
                {
                    command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = new Guid(messageId);

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                scope.Complete();
            }

        }

        public async Task<FailureInfoStorage.ProcessingFailureInfo> TryGetFailureInfoForMessage(SqlConnectionFactory connectionFactory, string messageId)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var command = new SqlCommand(Sql.GetFailuresForMessage, connection);
                command.Parameters.AddWithValue("@Id", messageId);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new FailureInfoStorage.ProcessingFailureInfo((int)reader["Counter"], ExceptionDispatchInfo.Capture(new Exception()));
                    }

                    return null;
                }
            }
        }

        public void ClearFailureInfoForMessage(SqlConnectionFactory connectionFactory, string messageId)
        {
            /* no op for now */
        }
    }
}