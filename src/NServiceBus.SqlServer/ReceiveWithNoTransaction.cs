namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class ReceiveWithNoTransaction : ReceiveStrategy
    {
        readonly string connectionString;

        public ReceiveWithNoTransaction(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = new SqlConnection(this.connectionString))
            {
                sqlConnection.Open();
                var readResult = inputQueue.TryReceive(sqlConnection, null);

                if (readResult.IsPoison)
                {
                    errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, null);
                    return;
                }

                if (readResult.Successful)
                {
                    var message = readResult.Message;

                    using (var bodyStream = message.BodyStream)
                    {
                        bodyStream.Position = 0;

                        var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new ContextBag ());
                        pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.NoTransaction, Connection = sqlConnection});

                        await onMessage(pushContext).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}