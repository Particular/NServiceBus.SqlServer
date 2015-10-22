using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Extensibility;

namespace NServiceBus.Transports.SQLServer.Light
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.Routing;
    using NServiceBus.Serializers.Json;

    class SqlServerMessageSender : IDispatchMessages
    {
        readonly string connectionString;

        public SqlServerMessageSender(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public Task Dispatch(IEnumerable<TransportOperation> outgoingMessages, ContextBag context)
        {
            foreach (var outgoingMessage in outgoingMessages)
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

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    var commandText = string.Format(SqlSend, "dbo", destination);

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

            return Task.FromResult(0);
        }

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

    }
}