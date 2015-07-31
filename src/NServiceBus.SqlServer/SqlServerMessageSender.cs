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
        readonly PipelineExecutor pipelineExecutor;

        public SqlServerMessageSender(IConnectionStringProvider connectionStringProvider, PipelineExecutor pipelineExecutor)
        {
            this.connectionStringProvider = connectionStringProvider;
            this.pipelineExecutor = pipelineExecutor;
        }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            Address destination = null;
            try
            {
                destination = DetermineDestination(sendOptions);
            
                var connectionInfo = connectionStringProvider.GetForDestination(sendOptions.Destination);
                var queue = new TableBasedQueue(destination, connectionInfo.Schema);
                if (sendOptions.EnlistInReceiveTransaction)
                {
                    SqlTransaction currentTransaction;
                    if (pipelineExecutor.TryGetTransaction(connectionInfo.ConnectionString, out currentTransaction))
                    {
                        queue.Send(message, sendOptions, currentTransaction.Connection, currentTransaction);
                    }
                    else
                    {
                        SqlConnection currentConnection;
                        if (pipelineExecutor.TryGetConnection(connectionInfo.ConnectionString, out currentConnection))
                        {
                            queue.Send(message, sendOptions, currentConnection);
                        }
                        else
                        {
                            using (var connection = SqlConnectionFactory.OpenNewConnection(connectionInfo.ConnectionString))
                            {
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
                        using (var connection = SqlConnectionFactory.OpenNewConnection(connectionInfo.ConnectionString))
                        {
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

        static void ThrowQueueNotFoundException(Address destination, SqlException ex)
        {
            var msg = destination == null
                ? "Failed to send message. Target address is null."
                : string.Format("Failed to send message to address: [{0}]", destination);

            throw new QueueNotFoundException(destination, msg, ex);
        }

        Address DetermineDestination(SendOptions sendOptions)
        {
            return RequestorProvidedCallbackAddress(sendOptions) ?? SenderProvidedDestination(sendOptions);
        }

        static Address SenderProvidedDestination(SendOptions sendOptions)
        {
            return sendOptions.Destination;
        }

        Address RequestorProvidedCallbackAddress(SendOptions sendOptions)
        {
            return IsReply(sendOptions) 
                ? pipelineExecutor.CurrentContext.TryGetCallbackAddress() 
                : null;
        }

        static bool IsReply(SendOptions sendOptions)
        {
            return sendOptions.GetType().FullName.EndsWith("ReplyOptions");
        }

        static void ThrowFailedToSendException(Address address, Exception ex)
        {
            if (address == null)
            {
                throw new Exception("Failed to send message.", ex);
            }

            throw new Exception(
                string.Format("Failed to send message to address: {0}@{1}", address.Queue, address.Machine), ex);
        }

        
    }
}