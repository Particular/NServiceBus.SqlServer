namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class ReceiveWithNoTransaction : ReceiveStrategy
    {
        readonly SqlConnectionFactory connectionFactory;

        public ReceiveWithNoTransaction(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = await connectionFactory.OpenNewConnection())
            {
                var readResult = await inputQueue.TryReceive(sqlConnection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    await errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, null).ConfigureAwait(false);
                    return;
                }

                if (readResult.Successful)
                {
                    var message = readResult.Message;

                    using (var bodyStream = message.BodyStream)
                    {
                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new NullTransaction(), new ContextBag ());
                        pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.NoTransaction, Connection = sqlConnection});

                        await onMessage(pushContext).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}