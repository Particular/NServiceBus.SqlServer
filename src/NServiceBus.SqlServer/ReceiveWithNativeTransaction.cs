namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;

    class ReceiveWithNativeTransaction : ReceiveStrategy
    {
        readonly string connectionString;

        public ReceiveWithNativeTransaction(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public async Task ReceiveMessage(string messageId, TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = new SqlConnection(this.connectionString))
            {
                sqlConnection.Open();
                using (var transaction = sqlConnection.BeginTransaction())
                {
                    try
                    {
                        var readResult = inputQueue.TryReceive(messageId, sqlConnection, transaction);

                        if (readResult.IsPoison)
                        {
                            errorQueue.Send(readResult.DataRecord, sqlConnection, transaction);
                            transaction.Commit();
                            return;
                        }

                        if (readResult.Successful)
                        {
                            var message = readResult.Message;

                            using (var bodyStream = message.BodyStream)
                            {
                                bodyStream.Position = 0;
                                var pushContext = new PushContext(message.Id, message.Headers, bodyStream, new ContextBag());
                                
                                await onMessage(pushContext).ConfigureAwait(false);

                                transaction.Commit();
                                return;
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
