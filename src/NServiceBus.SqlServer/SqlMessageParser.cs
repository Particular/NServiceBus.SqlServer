namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using NServiceBus.Serializers.Json;

    static class SqlMessageParser
    {
        static JsonMessageSerializer headerSerializer = new JsonMessageSerializer(null);

        internal static SqlMessage ParseRawData(object[] rowData)
        {
            var transportId = rowData[0].ToString();

            DateTime? expireDateTime = null;
            if (rowData[Sql.Columns.TimeToBeReceived.Index] != DBNull.Value)
            {
                expireDateTime = (DateTime) rowData[Sql.Columns.TimeToBeReceived.Index];
            }

            var headers = GetHeaders(rowData);

            var body = GetNullableValue<byte[]>(rowData[Sql.Columns.Body.Index]) ?? new byte[0];

            var memoryStream = new MemoryStream(body);

            var message = new SqlMessage(transportId, expireDateTime, headers, memoryStream);

            var replyToAddress = GetNullableValue<string>(rowData[Sql.Columns.ReplyToAddress.Index]);

            if (!string.IsNullOrEmpty(replyToAddress))
            {
                message.Headers[Headers.ReplyToAddress] = replyToAddress;
            }

            return message;
        }

        static Dictionary<string, string> GetHeaders(object[] rowData)
        {
            var headersAsString = (string) rowData[Sql.Columns.Headers.Index];
            if (string.IsNullOrWhiteSpace(headersAsString))
            {
                return new Dictionary<string, string>();
            }
            return (Dictionary<string, string>) headerSerializer.DeserializeObject(headersAsString, typeof(Dictionary<string, string>));
        }

        internal static object[] CreateRawMessageData(OutgoingMessage message)
        {
            var data = new object[7];

            data[Sql.Columns.Id.Index] = Guid.NewGuid();

            string correlationId;
            if (message.Headers.TryGetValue(Headers.CorrelationId, out correlationId))
            {
                data[Sql.Columns.CorrelationId.Index] = correlationId;
            }
            else
            {
                data[Sql.Columns.CorrelationId.Index] = DBNull.Value;
            }

            string replyToAddress;
            if (message.Headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
            {
                data[Sql.Columns.ReplyToAddress.Index] = replyToAddress;
            }
            else
            {
                data[Sql.Columns.ReplyToAddress.Index] = DBNull.Value;
            }

            data[Sql.Columns.Recoverable.Index] = true;

            data[Sql.Columns.TimeToBeReceived.Index] = DBNull.Value;
            if (message.Headers.ContainsKey(Headers.TimeToBeReceived))
            {
                TimeSpan TTBR;
                if (TimeSpan.TryParse(message.Headers[Headers.TimeToBeReceived], out TTBR) && TTBR != TimeSpan.MaxValue)
                {
                    data[Sql.Columns.TimeToBeReceived.Index] = DateTime.UtcNow.Add(TTBR);
                }
            }

            data[Sql.Columns.Headers.Index] = new JsonMessageSerializer(null).SerializeObject(message.Headers);

            if (message.Body == null)
            {
                data[Sql.Columns.Body.Index] = DBNull.Value;
            }
            else
            {
                data[Sql.Columns.Body.Index] = message.Body;
            }

            return data;
        }

        static T GetNullableValue<T>(object value)
        {
            if (value == DBNull.Value)
            {
                return default(T);
            }
            return (T) value;
        }
    }
}