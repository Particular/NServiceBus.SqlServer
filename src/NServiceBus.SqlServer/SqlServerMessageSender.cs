namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Transactions;
    using Pipeline;
    using Unicast;
    using Unicast.Queuing;

    /// <summary>
    ///     SqlServer implementation of <see cref="ISendMessages" />.
    /// </summary>
    class SqlServerMessageSender : ISendMessages
    {
        public SqlServerMessageSender()
        {
            ConnectionStringCollection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string DefaultConnectionString { get; set; }

        public Dictionary<string, string> ConnectionStringCollection { get; set; }

        public PipelineExecutor PipelineExecutor { get; set; }

        public string CallbackQueue { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            var address = sendOptions.Destination;
            var connectionStringKey = sendOptions.Destination.Queue;
            string callbackAddress;

            if (sendOptions.GetType().FullName.EndsWith("ReplyOptions") &&
                message.Headers.TryGetValue(CallbackHeaderKey, out callbackAddress))
            {
                address = Address.Parse(callbackAddress);
            }

            //set our callback address
            if (!string.IsNullOrEmpty(CallbackQueue))
            {
                message.Headers[CallbackHeaderKey] = CallbackQueue;
            }
            try
            {
                //If there is a connectionstring configured for the queue, use that connectionstring
                var queueConnectionString = DefaultConnectionString;
                if (ConnectionStringCollection.Keys.Contains(connectionStringKey))
                {
                    queueConnectionString = ConnectionStringCollection[connectionStringKey];
                }
                var queue = new TableBasedQueue(address);
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
                    var msg = address == null
                        ? "Failed to send message. Target address is null."
                        : string.Format("Failed to send message to address: [{0}]", address);

                    throw new QueueNotFoundException(address, msg, ex);
                }

                ThrowFailedToSendException(address, ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(address, ex);
            }
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