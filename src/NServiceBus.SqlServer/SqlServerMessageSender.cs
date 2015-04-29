namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Transactions;
    using Pipeline;
    using Unicast;
    using Unicast.Queuing;

    class SqlServerMessageSender : ISendMessages
    {
        readonly IConnectionStringProvider connectionStringProvider;
        readonly BehaviorContext context;

        public SqlServerMessageSender(IConnectionStringProvider connectionStringProvider, BehaviorContext context)
        {
            this.connectionStringProvider = connectionStringProvider;
            this.context = context;
        }

        public void Send(OutgoingMessage message, SendOptions sendOptions)
        {
            string destination = null;
            try
            {
                destination = DetermineDestination(sendOptions);
            
                var connectionInfo = connectionStringProvider.GetForDestination(sendOptions.Destination);
                var queue = new TableBasedQueue(destination, connectionInfo.Schema);
                if (sendOptions.EnlistInReceiveTransaction)
                {
                    SqlTransaction currentTransaction;
                    if (context.TryGetTransaction(connectionInfo.ConnectionString, out currentTransaction))
                    {
                        queue.Send(message, sendOptions, currentTransaction.Connection, currentTransaction);
                    }
                    else
                    {
                        SqlConnection currentConnection;
                        if (context.TryGetConnection(connectionInfo.ConnectionString, out currentConnection))
                        {
                            queue.Send(message, sendOptions, currentConnection);
                        }
                        else
                        {
                            using (var connection = new SqlConnection(connectionInfo.ConnectionString))
                            {
                                connection.Open();
                                queue.Send(message, sendOptions, connection);
                            }
                        }
                    }
                }
                else 
                {
                    // Suppress so that even if DTC is on, we won't escalate
                    using (var tx = new TransactionScope(TransactionScopeOption.Suppress))
                    {
                        using (var connection = new SqlConnection(connectionInfo.ConnectionString))
                        {
                            connection.Open();
                            queue.Send(message, sendOptions, connection);
                        }

                        tx.Complete();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 208)
                {
                    ThrowQueueNotFoundException(destination, ex);
                }

                ThrowFailedToSendException(destination, ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(destination, ex);
            }
        }

        static void ThrowQueueNotFoundException(string destination, SqlException ex)
        {
            var msg = destination == null
                ? "Failed to send message. Target address is null."
                : string.Format("Failed to send message to address: [{0}]", destination);

            throw new QueueNotFoundException(destination, msg, ex);
        }

        string DetermineDestination(SendOptions sendOptions)
        {
            return RequestorProvidedCallbackAddress(sendOptions) ?? SenderProvidedDestination(sendOptions);
        }

        static string SenderProvidedDestination(SendOptions sendOptions)
        {
            return sendOptions.Destination;
        }

        string RequestorProvidedCallbackAddress(SendOptions sendOptions)
        {
            return IsReply(sendOptions)
                ? context.TryGetCallbackAddress() 
                : null;
        }

        static bool IsReply(SendOptions sendOptions)
        {
            return sendOptions.GetType().FullName.EndsWith("ReplyOptions");
        }

        static void ThrowFailedToSendException(string address, Exception ex)
        {
            if (address == null)
            {
                throw new Exception("Failed to send message.", ex);
            }

            throw new Exception(
                string.Format("Failed to send message to address: {0}", address), ex);
        }

        
    }
}