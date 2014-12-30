namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.Logging;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Unicast;

    class TableBasedQueue
    {
        public TableBasedQueue(string queueName, string schema)
        {
            tableName = queueName.GetTableName();
            this.queueName = queueName;
            this.schema = schema;
        }

        public string QueueName
        {
            get { return queueName; }
        }

        public void Send(TransportMessage message, SendOptions sendOptions, SqlConnection connection, SqlTransaction transaction = null)
        {
            var messageData = ExtractTransportMessageData(message, sendOptions);

            Send(messageData, connection, transaction);
        }

        public void Send(object[] messageData, SqlConnection connection, SqlTransaction transaction = null)
        {
            if (messageData.Length != Parameters.Length)
            {
                throw new InvalidOperationException("The length of message data array must match the name of Parameters array.");
            }
            using (var command = new SqlCommand(string.Format(SqlSend, schema, tableName), connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                ExecuteSendQuery(messageData, command);
            }
        }

        static void ExecuteSendQuery(object[] messageData, SqlCommand command)
        {            
            for (var i = 0; i < messageData.Length; i++)
            {
                command.Parameters.Add(Parameters[i], ParameterTypes[i]).Value = messageData[i];
            }

            command.ExecuteNonQuery();
        }

        static object[] ExtractTransportMessageData(TransportMessage message, SendOptions sendOptions)
        {
            var data = new object[7];

            data[IdColumn] = Guid.Parse(message.Id);
            data[CorrelationIdColumn] = GetValue(message.CorrelationId);
            if (sendOptions.ReplyToAddress != null)
            {
                data[ReplyToAddressColumn] = sendOptions.ReplyToAddress.ToString();
            }
            else if (message.ReplyToAddress != null)
            {
                data[ReplyToAddressColumn] = message.ReplyToAddress.ToString();
            }
            else
            {
                data[ReplyToAddressColumn] = DBNull.Value;
            }
            data[RecoverableColumn] = message.Recoverable;
            if (message.TimeToBeReceived == TimeSpan.MaxValue)
            {
                data[TimeToBeReceivedColumn] = DBNull.Value;
            }
            else
            {
                data[TimeToBeReceivedColumn] = DateTime.UtcNow.Add(message.TimeToBeReceived);
            }
            data[HeadersColumn] = HeaderSerializer.SerializeObject(message.Headers);
            if (message.Body == null)
            {
                data[BodyColumn] = DBNull.Value;
            }
            else
            {
                data[BodyColumn] = message.Body;
            }
            return data;
        }

        public MessageReadResult TryReceive(SqlConnection connection, SqlTransaction transaction = null)
        {
            return ReceiveWithNativeTransaction(string.Format(SqlReceive, schema, tableName), connection, transaction);
        }

        MessageReadResult ReceiveWithNativeTransaction(string sql, SqlConnection connection, SqlTransaction transaction)
        {
            using (var command = new SqlCommand(sql, connection, transaction)
            {
                CommandType = CommandType.Text
            })
            {
                return ExecuteReader(command);
            }
        }

        MessageReadResult ExecuteReader(SqlCommand command)
        {
            object[] rowData;
            using (var dataReader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (dataReader.Read())
                {
                    rowData = new object[dataReader.FieldCount];
// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                    dataReader.GetValues(rowData);
                }
                else
                {
                    return MessageReadResult.NoMessage;
                }
            }

            try
            {
                var id = rowData[0].ToString();

                DateTime? expireDateTime = null;
                if (rowData[TimeToBeReceivedColumn] != DBNull.Value)
                {
                    expireDateTime = (DateTime)rowData[TimeToBeReceivedColumn];
                }

                //Has message expired?
                if (expireDateTime.HasValue && expireDateTime.Value < DateTime.UtcNow)
                {
                    Logger.InfoFormat("Message with ID={0} has expired. Removing it from queue.", id);
                    return MessageReadResult.NoMessage;
                }

                var headers = (Dictionary<string, string>)HeaderSerializer.DeserializeObject((string)rowData[HeadersColumn], typeof(Dictionary<string, string>));
                var correlationId = GetNullableValue<string>(rowData[CorrelationIdColumn]);
                var recoverable = (bool)rowData[RecoverableColumn];
                var body = GetNullableValue<byte[]>(rowData[BodyColumn]);

                var message = new TransportMessage(id, headers)
                {
                    CorrelationId = correlationId,
                    Recoverable = recoverable,
                    Body = body ?? new byte[0]
                };

                var replyToAddress = GetNullableValue<string>(rowData[ReplyToAddressColumn]);

                if (!string.IsNullOrEmpty(replyToAddress))
                {
                    message.Headers[Headers.ReplyToAddress] = replyToAddress;
                }

                if (expireDateTime.HasValue)
                {
                    message.TimeToBeReceived = TimeSpan.FromTicks(expireDateTime.Value.Ticks - DateTime.UtcNow.Ticks);
                }

                return MessageReadResult.Success(message);
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return MessageReadResult.Poison(rowData);
            }
        }

        static object GetValue(object value)
        {
            return value ?? DBNull.Value;
        }

        static T GetNullableValue<T>(object value)
        {
            if (value == DBNull.Value)
            {
                return default(T);
            }
            return (T)value;
        }

        public override string ToString()
        {
            return tableName;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(TableBasedQueue));

        readonly string tableName;
        readonly string queueName;
        readonly string schema;
        static  readonly JsonMessageSerializer HeaderSerializer = new JsonMessageSerializer(null);

        static readonly string[] Parameters = { "Id", "CorrelationId", "ReplyToAddress", "Recoverable", "Expires", "Headers", "Body" };

        static readonly SqlDbType[] ParameterTypes =
        {
            SqlDbType.UniqueIdentifier,
            SqlDbType.VarChar,
            SqlDbType.VarChar,
            SqlDbType.Bit,
            SqlDbType.DateTime,
            SqlDbType.VarChar,
            SqlDbType.VarBinary
        };



        const string SqlSend =
            @"INSERT INTO [{0}].[{1}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";

        const string SqlReceive =
            @"WITH message AS (SELECT TOP(1) * FROM [{0}].[{1}] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion] ASC) 
			DELETE FROM message 
			OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, 
			deleted.Recoverable, deleted.Expires, deleted.Headers, deleted.Body;";

        const int IdColumn = 0;
        const int CorrelationIdColumn = 1;
        const int ReplyToAddressColumn = 2;
        const int RecoverableColumn = 3;
        const int TimeToBeReceivedColumn = 4;
        const int HeadersColumn = 5;
        const int BodyColumn = 6;
    }
}