namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using Serializers.Json;
    using Unicast.Queuing;

    /// <summary>
    ///     SqlServer implementation of <see cref="ISendMessages" />.
    /// </summary>
    public class SqlServerMessageSender : ISendMessages
    {
        const string SqlSend =
            @"INSERT INTO [{0}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";

        static JsonMessageSerializer Serializer = new JsonMessageSerializer(null);

        public string DefaultConnectionString { get; set; }

        public Dictionary<string, string> ConnectionStringCollection { get; private set; }

        public UnitOfWork UnitOfWork { get; set; }
        
        public SqlServerMessageSender()
        {
            ConnectionStringCollection = new Dictionary<string, string>();
        }

        /// <summary>
        ///     Sends the given <paramref name="message" /> to the <paramref name="address" />.
        /// </summary>
        /// <param name="message">
        ///     <see cref="TransportMessage" /> to send.
        /// </param>
        /// <param name="address">
        ///     Destination <see cref="Address" />.
        /// </param>
        public void Send(TransportMessage message, Address address)
        {
            try
            {

                //If there is a connectionstring configured for the queue, use that connectionstring
                string queueConnectionString = DefaultConnectionString;
                if (ConnectionStringCollection.Keys.Contains(address.Queue))
                {
                    queueConnectionString = ConnectionStringCollection[address.Queue];
                }

                //Refactor to HasAndCanUseActiveTransaction?
                if (UnitOfWork.HasActiveTransaction() &&
                    UnitOfWork.TransactionUsesTheSameConnectionString(queueConnectionString))
                {
                    //if there is an active transaction and the connectionstrings are equal, we can use the same transaction
                    using (
                        var command = new SqlCommand(string.Format(SqlSend, address.Queue),
                                                        UnitOfWork.Transaction.Connection, UnitOfWork.Transaction)
                        {
                            CommandType = CommandType.StoredProcedure
                        })
                    {
                        ExecuteQuery(message, command);
                    }
                }
                else
                {
                    //When there is no transaction or a DTC transaction we use a new connection
                    using (var connection = new SqlConnection(queueConnectionString))
                    {
                        connection.Open();
                        using (var command = new SqlCommand(string.Format(SqlSend, address.Queue), connection)
                            {
                                CommandType = CommandType.Text
                            })
                        {
                            ExecuteQuery(message, command);
                        }
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


        private static void ThrowFailedToSendException(Address address, Exception ex)
        {
            if (address == null)
                throw new FailedToSendMessageException("Failed to send message.", ex);

            throw new FailedToSendMessageException(
                string.Format("Failed to send message to address: {0}@{1}", address.Queue, address.Machine), ex);
        }

        private static void ExecuteQuery(TransportMessage message, SqlCommand command)
        {
            command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = Guid.Parse(message.Id);
            command.Parameters.Add("CorrelationId", SqlDbType.VarChar).Value =
                GetValue(message.CorrelationId);
            if (message.ReplyToAddress == null) // SendOnly endpoint
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value = DBNull.Value;
            }
            else
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value =
                    message.ReplyToAddress.ToString();
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

        private static object GetValue(object value)
        {
            return value ?? DBNull.Value;
        }
    }
}