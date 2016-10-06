namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class ReceiveWithNoTransaction : ReceiveStrategy
    {
        public ReceiveWithNoTransaction(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var readResult = await InputQueue.TryReceive(connection, null).ConfigureAwait(false);

                if (readResult.IsPoison)
                {
                    await ErrorQueue.DeadLetter(readResult.PoisonMessage, connection, null).ConfigureAwait(false);
                    return;
                }

                if (!readResult.Successful)
                {
                    receiveCancellationTokenSource.Cancel();
                    return;
                }

                var message = readResult.Message;
                if (message.Destination != null)
                {
                    //Forward the message
                    var destinationQueue = QueueFactory(message.Destination);
                    await destinationQueue.Send(new OutgoingMessage(message.TransportId, message.Headers, message.Body), connection, null).ConfigureAwait(false);
                    return;
                }

                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(connection);

                try
                {
                    await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    await HandleError(exception, message, transportTransaction, 1).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}