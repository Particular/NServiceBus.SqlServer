namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Transactions;
    using Pipeline;
    using Serializers.Json;
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
        public bool SchemaAwareAddressing { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            var address = sendOptions.Destination;

            string callbackAddress;

            if (message.MessageIntent == MessageIntentEnum.Reply &&
                message.Headers.TryGetValue(CallbackHeaderKey, out callbackAddress))
            {
                address = Address.Parse(callbackAddress);
            }

            //set our callback address
            message.Headers[CallbackHeaderKey] = CallbackQueue;

            var queue = address.Queue;
            try
            {
                //If there is a connectionstring configured for the queue, use that connectionstring
                var queueConnectionString = DefaultConnectionString;
                if (ConnectionStringCollection.Keys.Contains(queue))
                {
                    queueConnectionString = ConnectionStringCollection[queue];
                }

                if (sendOptions.EnlistInReceiveTransaction)
                {
                    SqlTransaction currentTransaction;

                    if (PipelineExecutor.CurrentContext.TryGet(string.Format("SqlTransaction-{0}", queueConnectionString), out currentTransaction))
                    {
                        using (var command = new SqlCommand(string.Format(SqlSend, address.GetTableName(SchemaAwareAddressing)), currentTransaction.Connection, currentTransaction)
                        {
                            CommandType = CommandType.Text
                        })
                        {
                            ExecuteQuery(message, command, sendOptions);
                        }
                    }
                    else
                    {
                        SqlConnection currentConnection;

                        if (PipelineExecutor.CurrentContext.TryGet(string.Format("SqlConnection-{0}", queueConnectionString), out currentConnection))
                        {
                            ExecuteSendCommand(message, address, currentConnection, sendOptions);
                        }
                        else
                        {
                            using (var connection = new SqlConnection(queueConnectionString))
                            {
                                connection.Open();
                                ExecuteSendCommand(message, address, connection, sendOptions);
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
                            ExecuteSendCommand(message, address, connection, sendOptions);
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

        void ExecuteSendCommand(TransportMessage message, Address address, SqlConnection connection, SendOptions sendOptions)
        {
            using (var command = new SqlCommand(string.Format(SqlSend, address.GetTableName(SchemaAwareAddressing)), connection)
            {
                CommandType = CommandType.Text
            })
            {
                ExecuteQuery(message, command, sendOptions);
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

        static void ExecuteQuery(TransportMessage message, SqlCommand command, SendOptions sendOptions)
        {
            command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = Guid.Parse(message.Id);
            command.Parameters.Add("CorrelationId", SqlDbType.VarChar).Value =
                GetValue(message.CorrelationId);
            if (sendOptions.ReplyToAddress != null)
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value =
                    sendOptions.ReplyToAddress.ToString();
            }
            else if (message.ReplyToAddress != null)
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value =
                    message.ReplyToAddress.ToString();
            }
            else
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value = DBNull.Value;
            }
            command.Parameters.Add("Recoverable", SqlDbType.Bit).Value = message.Recoverable;
            if (message.TimeToBeReceived == TimeSpan.MaxValue)
            {
                command.Parameters.Add("Expires", SqlDbType.DateTime).Value = DBNull.Value;
            }
            else
            {
                command.Parameters.Add("Expires", SqlDbType.DateTime).Value =
                    DateTime.UtcNow.Add(message.TimeToBeReceived);
            }
            command.Parameters.Add("Headers", SqlDbType.VarChar).Value =
                Serializer.SerializeObject(message.Headers);
            if (message.Body == null)
            {
                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = DBNull.Value;
            }
            else
            {
                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = message.Body;
            }

            command.ExecuteNonQuery();
        }

        static object GetValue(object value)
        {
            return value ?? DBNull.Value;
        }

        const string SqlSend =
            @"INSERT INTO {0} ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";

        public const string CallbackHeaderKey = "NServiceBus.SqlServer.CallbackQueue";

        static JsonMessageSerializer Serializer = new JsonMessageSerializer(null);
    }
}