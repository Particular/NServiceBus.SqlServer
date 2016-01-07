namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class ReceiveWithNoTransaction : ReceiveStrategy
    {
        readonly SqlConnectionFactory connectionFactory;
        readonly EndpointConnectionStringLookup endpointConnectionStringLookup;

        public ReceiveWithNoTransaction(SqlConnectionFactory connectionFactory, EndpointConnectionStringLookup endpointConnectionStringLookup)
        {
            this.connectionFactory = connectionFactory;
            this.endpointConnectionStringLookup = endpointConnectionStringLookup;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, CancellationTokenSource cancellationTokenSource, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var inputConnection = await connectionFactory.OpenNewConnection(connectionString))
            {
                var readResult = await inputQueue.TryReceive(inputConnection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    var errorConnectionString = endpointConnectionStringLookup.ConnectionStringLookup(errorQueue.ToString()).GetAwaiter().GetResult();

                    {
                        await errorQueue.SendRawMessage(readResult.DataRecord, errorConnection, null).ConfigureAwait(false);
                        return;
                    }
                }

                if (readResult.Successful)
                {
                    var message = readResult.Message;

                    using (var bodyStream = message.BodyStream)
                    {
                        var transportTransaction = new TransportTransaction();
                        transportTransaction.Set(inputConnection);

                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, transportTransaction, cancellationTokenSource, new ContextBag());

                        await onMessage(pushContext).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}