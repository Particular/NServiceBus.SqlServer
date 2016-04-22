namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;

    static class MessageParser
    {
        // We are assuming sequential access, order is important
        internal static async Task<Message> ParseRawData(SqlDataReader dataReader)
        {
            var transportId = (await dataReader.GetFieldValueAsync<Guid>(Sql.Columns.Id.Index).ConfigureAwait(false)).ToString();

            // Wwe need to read all even if not used!
            await GetNullableValueAsync<string>(dataReader, Sql.Columns.CorrelationId.Index).ConfigureAwait(false);

            var replyToAddress = await GetNullableValueAsync<string>(dataReader, Sql.Columns.ReplyToAddress.Index).ConfigureAwait(false);

            // We need to read all even if not used!
            await dataReader.GetFieldValueAsync<bool>(Sql.Columns.Recoverable.Index).ConfigureAwait(false);

            int? millisecondsToExpiry = null;
            if (!dataReader.IsDBNull(Sql.Columns.TimeToBeReceived.Index))
            {
                millisecondsToExpiry = await dataReader.GetFieldValueAsync<int>(Sql.Columns.TimeToBeReceived.Index).ConfigureAwait(false);
            }

            var headers = await GetHeaders(dataReader, Sql.Columns.Headers.Index).ConfigureAwait(false);

            var bodyStream = await GetBody(dataReader, Sql.Columns.Body.Index).ConfigureAwait(false);

            var message = new Message(transportId, millisecondsToExpiry, headers, bodyStream);

            if (!string.IsNullOrEmpty(replyToAddress))
            {
                message.Headers[Headers.ReplyToAddress] = replyToAddress;
            }

            return message;
        }

        static async Task<Dictionary<string, string>> GetHeaders(SqlDataReader dataReader, int headersIndex)
        {
            if (await dataReader.IsDBNullAsync(headersIndex).ConfigureAwait(false))
            {
                return new Dictionary<string, string>();
            }

            using (var textReader = dataReader.GetTextReader(headersIndex))
            {
                var headersAsString = await textReader.ReadToEndAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(headersAsString))
                {
                    return new Dictionary<string, string>();
                }
                return DictionarySerializer.DeSerialize(headersAsString);
            }
        }

        static async Task<MemoryStream> GetBody(SqlDataReader dataReader, int bodyIndex)
        {
            var memoryStream = new MemoryStream();
            // Null values will be returned as an empty (zero bytes) Stream.
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            }
            memoryStream.Position = 0;
            return memoryStream;
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

        static async Task<T> GetNullableValueAsync<T>(SqlDataReader dataReader, int index)
        {
            if (await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return default(T);
            }
            return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
        }
    }
}