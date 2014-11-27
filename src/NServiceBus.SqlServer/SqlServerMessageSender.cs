namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Transactions;
    using Pipeline;
    using Unicast;
    using Unicast.Queuing;

    /// <summary>
    ///     SqlServer implementation of <see cref="ISendMessages" />.
    /// </summary>
    class SqlServerMessageSender : ISendMessages
    {
        public string DefaultConnectionString { get; set; }

        public IConnectionStringProvider ConnectionStringProvider { get; set; }

        public PipelineExecutor PipelineExecutor { get; set; }

        public string CallbackQueue { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            Address destination = null;
            try
            {
                destination = DetermineDestination(message, sendOptions);
                SetCallbackAddress(message);
            
                var queueConnectionString = ConnectionStringProvider.GetForDestination(sendOptions.Destination);
                var queue = new TableBasedQueue(destination);
                if (sendOptions.EnlistInReceiveTransaction)
                {
                    SqlTransaction currentTransaction;
                    if (PipelineExecutor.TryGetTransaction(queueConnectionString, out currentTransaction))
                    {
                        queue.Send(message, sendOptions, currentTransaction.Connection, currentTransaction);
                    }
                    else
                    {
                        SqlConnection currentConnection;
                        if (PipelineExecutor.TryGetConnection(queueConnectionString, out currentConnection))
                        {
                            queue.Send(message, sendOptions, currentConnection);
                        }
                        else
                        {
                            using (var connection = new SqlConnection(queueConnectionString))
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
                        using (var connection = new SqlConnection(queueConnectionString))
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
                    var msg = destination == null
                        ? "Failed to send message. Target address is null."
                        : string.Format("Failed to send message to address: [{0}]", destination);

                    throw new QueueNotFoundException(destination, msg, ex);
                }

                ThrowFailedToSendException(destination, ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(destination, ex);
            }
        }

        void SetCallbackAddress(TransportMessage message)
        {
            if (!string.IsNullOrEmpty(CallbackQueue))
            {
                message.Headers[CallbackHeaderKey] = CallbackQueue;
            }
        }

        static Address DetermineDestination(TransportMessage message, SendOptions sendOptions)
        {
            return RequestorProvidedCallbackAddress(message, sendOptions) ?? SenderProvidedDestination(sendOptions);
        }

        static Address SenderProvidedDestination(SendOptions sendOptions)
        {
            return sendOptions.Destination;
        }

        static Address RequestorProvidedCallbackAddress(TransportMessage message, SendOptions sendOptions)
        {
            string callbackAddress;
            return IsReply(sendOptions) && message.Headers.TryGetValue(CallbackHeaderKey, out callbackAddress) 
                ? Address.Parse(callbackAddress) 
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

        public const string CallbackHeaderKey = "NServiceBus.SqlServer.CallbackQueue";
    }
}