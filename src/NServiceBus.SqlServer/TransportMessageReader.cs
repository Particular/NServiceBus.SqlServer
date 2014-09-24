namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.Logging;
    using NServiceBus.Serializers.Json;

    class TransportMessageReader
    {
        public  TransportMessage ExecuteReader(SqlCommand command)
        {
            using (var dataReader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (dataReader.Read())
                {
                    var id = dataReader.GetGuid(0).ToString();

                    DateTime? expireDateTime = null;
                    if (!dataReader.IsDBNull(4))
                    {
                        expireDateTime = dataReader.GetDateTime(4);
                    }

                    //Has message expired?
                    if (expireDateTime.HasValue && expireDateTime.Value < DateTime.UtcNow)
                    {
                        Logger.InfoFormat("Message with ID={0} has expired. Removing it from queue.", id);
                        return null;
                    }

                    var headers = (Dictionary<string, string>)Serializer.DeserializeObject(dataReader.GetString(5), typeof(Dictionary<string, string>));
                    var correlationId = dataReader.IsDBNull(1) ? null : dataReader.GetString(1);
                    var recoverable = dataReader.GetBoolean(3);
                    var body = dataReader.IsDBNull(6) ? null : dataReader.GetSqlBinary(6).Value;

                    var message = new TransportMessage(id, headers)
                    {
                        CorrelationId = correlationId,
                        Recoverable = recoverable,
                        Body = body ?? new byte[0]
                    };

                    var replyToAddress = dataReader.IsDBNull(2) ? null : dataReader.GetString(2);

                    if (!string.IsNullOrEmpty(replyToAddress))
                    {
                        message.Headers[Headers.ReplyToAddress] = replyToAddress;
                    }

                    if (expireDateTime.HasValue)
                    {
                        message.TimeToBeReceived = TimeSpan.FromTicks(expireDateTime.Value.Ticks - DateTime.UtcNow.Ticks);
                    }

                    return message;
                }
            }

            return null;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(TransportMessageReader));
        readonly JsonMessageSerializer Serializer = new JsonMessageSerializer(null);
    }
}