namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Serializers.Json;
    using NServiceBus.Transports.SQLServer.Light;

    class TableBasedQueue
    {
        readonly string connectionString;

        public TableBasedQueue(string tableName, string schema, string connectionString)
        {
            this.tableName = tableName;
            this.schema = schema;
            this.connectionString = connectionString;
        }

        public MessageReadResult TryReceive()
        {
            using (var connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(string.Format(SqlReceive, this.schema, this.tableName), connection)
                {
                    CommandType = CommandType.Text
                })
                {
                    return ExecuteReader(command);
                }

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


                var headers = (Dictionary<string, string>)HeaderSerializer.DeserializeObject((string)rowData[HeadersColumn], typeof(Dictionary<string, string>));
                var body = GetNullableValue<byte[]>(rowData[BodyColumn]) ?? new byte[0];

                var memoryStream = new MemoryStream(body);

                var message = new SqlMessage(id, memoryStream, headers);

                var replyToAddress = GetNullableValue<string>(rowData[ReplyToAddressColumn]);

                if (!String.IsNullOrEmpty(replyToAddress))
                {
                    message.Headers[Headers.ReplyToAddress] = replyToAddress;
                }

                return MessageReadResult.Success(message);
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return new MessageReadResult();
                //return MessageReadResult.Poison(rowData);
            }
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
        readonly string schema;
        static readonly JsonMessageSerializer HeaderSerializer = new JsonMessageSerializer(null);



        const string SqlReceive =
            @"WITH message AS (SELECT TOP(1) * FROM [{0}].[{1}] WITH (UPDLOCK, READPAST, ROWLOCK) ORDER BY [RowVersion] ASC) 
			DELETE FROM message 
			OUTPUT deleted.Id, deleted.CorrelationId, deleted.ReplyToAddress, 
			deleted.Recoverable, deleted.Expires, deleted.Headers, deleted.Body;";

        static object[] ExtractTransportMessageData(OutgoingMessage message)
        {
            var data = new object[7];

            data[IdColumn] = Guid.Parse(message.MessageId);
            data[CorrelationIdColumn] = message.Headers[Headers.CorrelationId];

            //TODO: where does Reply-To-Address come from
            string replyToAddress;
            if (message.Headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
            {
                data[ReplyToAddressColumn] = replyToAddress;
            }
            else
            {
                data[ReplyToAddressColumn] = DBNull.Value;
            }

            //TODO: figure out what recoverable means
            data[RecoverableColumn] = true;

            data[TimeToBeReceivedColumn] = DBNull.Value;

            data[HeadersColumn] = new JsonMessageSerializer(null).SerializeObject(message.Headers);

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

        const string SqlSend =
           @"INSERT INTO [{0}].[{1}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";

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

        const int IdColumn = 0;
        const int CorrelationIdColumn = 1;
        const int ReplyToAddressColumn = 2;
        const int RecoverableColumn = 3;
        const int TimeToBeReceivedColumn = 4;
        const int HeadersColumn = 5;
        const int BodyColumn = 6;

        public void SendMessage(TransportOperation outgoingMessage)
        {
            var messageData = ExtractTransportMessageData(outgoingMessage.Message);

            if (messageData.Length != Parameters.Length)
            {
                throw new InvalidOperationException("The length of message data array must match the name of Parameters array.");
            }

            var dispatchOptions = outgoingMessage.DispatchOptions;
            var routingStrategy = dispatchOptions.AddressTag as UnicastAddressTag;

            if (routingStrategy == null)
            {
                throw new Exception("The MSMQ transport only supports the `DirectRoutingStrategy`, strategy required " + dispatchOptions.AddressTag.GetType().Name);
            }

            var destination = routingStrategy.Destination;

            using (var connection = new SqlConnection(this.connectionString))
            {
                connection.Open();

                var commandText = String.Format(SqlSend, "dbo", destination);

                //TODO: figure out how tansactions are passed and are they only for native transactions
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = new SqlCommand(commandText, connection, transaction)
                    {
                        CommandType = CommandType.Text
                    })
                    {
                        for (var i = 0; i < messageData.Length; i++)
                        {
                            command.Parameters.Add(Parameters[i], ParameterTypes[i]).Value = messageData[i];
                        }

                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }
    }
}