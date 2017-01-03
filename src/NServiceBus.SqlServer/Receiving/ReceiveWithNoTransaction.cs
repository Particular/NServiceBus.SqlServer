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
                var message = await TryReceive(connection, null, receiveCancellationTokenSource).ConfigureAwait(false);
                if (message == null)
                {
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