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
        public async Task ReceiveMessage(TableBasedQueue inputQueue, TableBasedQueue errorQueue, Func<PushContext, Task> onMessage)
        {
            using (var sqlConnection = new SqlConnection(this.connectionString))
            {
                sqlConnection.Open();

                using (var transaction = sqlConnection.BeginTransaction())
                {
                    try
                    {
                        var readResult = inputQueue.TryReceive(sqlConnection, transaction);

                        if (readResult.IsPoison)
                        {
                            errorQueue.SendRawMessage(readResult.DataRecord, sqlConnection, transaction);
                            transaction.Commit();
                            return;
                        }

                        //TODO: IsPoision and Successful can probably be marged they are negations of each other
                        if (readResult.Successful)
                        {
                            var message = readResult.Message;

                            using (var bodyStream = message.BodyStream)
                            {
                                //TODO: do we really need to rewind here?
                                bodyStream.Position = 0;

                                var pushContext = new PushContext(message.TransportId, message.Headers, bodyStream, new ContextBag());
                                pushContext.Context.Set(new ReceiveContext {Type = ReceiveType.NativeTransaction, Transaction = transaction});

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
