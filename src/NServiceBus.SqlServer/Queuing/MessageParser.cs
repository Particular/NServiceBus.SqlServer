﻿namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;

    static class MessageParser
    {
        static byte[] EmptyBody = new byte[0];

        internal static async Task<Message> ParseRawData(SqlDataReader dataReader)
        {
            var transportId = (await dataReader.GetFieldValueAsync<Guid>(Sql.Columns.Id.Index).ConfigureAwait(false)).ToString();

            int? millisecondsToExpiry = null;
            if (!await dataReader.IsDBNullAsync(Sql.Columns.TimeToBeReceived.Index).ConfigureAwait(false))
            {
                millisecondsToExpiry = await dataReader.GetFieldValueAsync<int>(Sql.Columns.TimeToBeReceived.Index).ConfigureAwait(false);
            }

            var headers = await GetHeaders(dataReader).ConfigureAwait(false);

            var body = await GetNullableValue<byte[]>(dataReader, Sql.Columns.Body.Index).ConfigureAwait(false) ?? EmptyBody;

            var memoryStream = new MemoryStream(body);

            var message = new Message(transportId, millisecondsToExpiry, headers, memoryStream);

            var replyToAddress = await GetNullableValue<string>(dataReader, Sql.Columns.ReplyToAddress.Index).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(replyToAddress))
            {
                message.Headers[Headers.ReplyToAddress] = replyToAddress;
            }

            return message;
        }

        static async Task<Dictionary<string, string>> GetHeaders(SqlDataReader dataReader)
        {
            var headersAsString = await GetNullableValue<string>(dataReader, Sql.Columns.Headers.Index).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(headersAsString))
            {
                return new Dictionary<string, string>();
            }
            return DictionarySerializer.DeSerialize(headersAsString);
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
                    data[Sql.Columns.TimeToBeReceived.Index] = TTBR.TotalMilliseconds;
                }
            }

            data[Sql.Columns.Headers.Index] = DictionarySerializer.Serialize(message.Headers);

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

        static async Task<T> GetNullableValue<T>(SqlDataReader dataReader, int index)
        {
            if (!await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
            }
            return default(T);
        }
    }
}