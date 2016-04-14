namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

    class ReceiveWithNoTransaction : ReceiveStrategy
    {
        public ReceiveWithNoTransaction(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var readResult = await inputQueue.TryReceive(connection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    await errorQueue.SendRawMessage(readResult.DataRecord, connection, null).ConfigureAwait(false);

                    return;
                }

                if (readResult.Successful)
                {
                    var message = readResult.Message;

                    using (var bodyStream = message.BodyStream)
                    {
                        var transportTransaction = new TransportTransaction();
                        transportTransaction.Set(connection);

                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, cancellationTokenSource, new ContextBag());

                        await onMessage(pushContext).ConfigureAwait(false);
                    }
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}