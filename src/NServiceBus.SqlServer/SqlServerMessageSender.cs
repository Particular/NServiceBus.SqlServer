﻿namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Transactions;
    using Unicast;
    using Unicast.Queuing;

    class SqlServerMessageSender : ISendMessages
    {
        readonly IConnectionStringProvider connectionStringProvider;
        readonly IConnectionStore connectionStore;
        readonly ICallbackAddressStore callbackAddressStore;
        readonly ConnectionFactory sqlConnectionFactory;

        public SqlServerMessageSender(IConnectionStringProvider connectionStringProvider, IConnectionStore connectionStore, ICallbackAddressStore callbackAddressStore, ConnectionFactory sqlConnectionFactory)
        {
            this.connectionStringProvider = connectionStringProvider;
            this.connectionStore = connectionStore;
            this.callbackAddressStore = callbackAddressStore;
            this.sqlConnectionFactory = sqlConnectionFactory;
        }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            Address destination = null;
            try
            {
                destination = DetermineDestination(sendOptions);
            
                var connectionInfo = connectionStringProvider.GetForDestination(sendOptions.Destination);
                var queue = new TableBasedQueue(destination, OverrideSchema(destination, connectionInfo.Schema));
                if (sendOptions.EnlistInReceiveTransaction)
                {
                    SqlTransaction currentTransaction;
                    if (connectionStore.TryGetTransaction(connectionInfo.ConnectionString, out currentTransaction))
                    {
                        queue.Send(message, sendOptions, currentTransaction.Connection, currentTransaction);
                    }
                    else
                    {
                        SqlConnection currentConnection;
                        if (connectionStore.TryGetConnection(connectionInfo.ConnectionString, out currentConnection))
                        {
                            queue.Send(message, sendOptions, currentConnection);
                        }
                        else
                        {
                            using (var connection = sqlConnectionFactory.OpenNewConnection(connectionInfo.ConnectionString))
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
                        using (var connection = sqlConnectionFactory.OpenNewConnection(connectionInfo.ConnectionString))
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

        static string OverrideSchema(Address address, string defaultSchema)
        {
            if (string.IsNullOrEmpty(address.Machine))
            {
                return defaultSchema;
            }
            using (var sanitizer = new SqlCommandBuilder())
            {
                return sanitizer.UnquoteIdentifier(address.Machine);
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
            if (IsReply(sendOptions))
            {
                Address callbackAddress;
                callbackAddressStore.TryGetCallbackAddress(out callbackAddress);
                return callbackAddress;
            }
            
            return null;
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