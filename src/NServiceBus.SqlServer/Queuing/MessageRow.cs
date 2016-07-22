namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;
    using Logging;
    using static System.String;

    class MessageRow
    {
        MessageRow() { }

        public static async Task<MessageReadResult> Read(SqlDataReader dataReader)
        {
            var row = await ReadRow(dataReader).ConfigureAwait(false);

            var result = row.TryParse();

            return result;
        }

        public static MessageRow From(Dictionary<string, string> headers, byte[] body)
        {
            var row = new MessageRow();

            row.id = Guid.NewGuid();
            row.correlationId = TryGetHeaderValue(headers, Headers.CorrelationId, s => s);
            row.replyToAddress = TryGetHeaderValue(headers, Headers.ReplyToAddress, s => s);
            row.recoverable = true;
            row.timeToBeReceived = TryGetHeaderValue(headers, Headers.TimeToBeReceived, s =>
            {
                TimeSpan timeToBeReceived;
                return TimeSpan.TryParse(s, out timeToBeReceived)
                    ? (int?)timeToBeReceived.TotalMilliseconds
                    : null;
            });
            row.headers = DictionarySerializer.Serialize(headers);
            row.bodyBytes = body;

            return row;
        }


        public void PrepareSendCommand(SqlCommand command)
        {
            AddParameter(command, "Id", SqlDbType.UniqueIdentifier, id);
            AddParameter(command, "CorrelationId", SqlDbType.VarChar, correlationId);
            AddParameter(command, "ReplyToAddress", SqlDbType.VarChar, replyToAddress);
            AddParameter(command, "Recoverable", SqlDbType.Bit, recoverable);
            AddParameter(command, "TimeToBeReceivedMs", SqlDbType.Int, timeToBeReceived);
            AddParameter(command, "Headers", SqlDbType.VarChar, headers);
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes ?? bodyStream.ToArray());
        }

        static async Task<MessageRow> ReadRow(SqlDataReader dataReader)
        {
            var row = new MessageRow();

            //HINT: we are assuming that dataReader is sequential. Order or reads is important !
            row.id = await dataReader.GetFieldValueAsync<Guid>(0).ConfigureAwait(false);
            row.correlationId = await GetNullableAsync<string>(dataReader, 1).ConfigureAwait(false);
            row.replyToAddress = await GetNullableAsync<string>(dataReader, 2).ConfigureAwait(false);
            row.recoverable = await dataReader.GetFieldValueAsync<bool>(3).ConfigureAwait(false);
            row.headers = await GetHeaders(dataReader, 4).ConfigureAwait(false);
            row.bodyStream = await GetBody(dataReader, 5).ConfigureAwait(false);

            return row;
        }
       
        MessageReadResult TryParse()
        {
            try
            {
                var parsedHeaders = IsNullOrEmpty(headers)
                    ? new Dictionary<string, string>()
                    : DictionarySerializer.DeSerialize(headers);

                if (!IsNullOrEmpty(replyToAddress))
                {
                    parsedHeaders[Headers.ReplyToAddress] = replyToAddress;
                }

                LegacyCallbacks.SubstituteReplyToWithCallbackQueueIfExists(parsedHeaders);

                return MessageReadResult.Success(new Message(id.ToString(), parsedHeaders, bodyStream));
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return MessageReadResult.Poison(this);
            }
        }

        static T TryGetHeaderValue<T>(Dictionary<string, string> headers, string name, Func<string, T> conversion)
        {
            string text;
            if (!headers.TryGetValue(name, out text))
            {
                return default(T);
            }
            var value = conversion(text);
            return value;
        }

        static async Task<string> GetHeaders(SqlDataReader dataReader, int headersIndex)
        {
            if (await dataReader.IsDBNullAsync(headersIndex).ConfigureAwait(false))
            {
                return null;
            }

            using (var textReader = dataReader.GetTextReader(headersIndex))
            {
                var headersAsString = await textReader.ReadToEndAsync().ConfigureAwait(false);
                return headersAsString;
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

        static async Task<T> GetNullableAsync<T>(SqlDataReader dataReader, int index) where T : class
        {
            if (await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return default(T);
            }

            return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
        }

        void AddParameter(SqlCommand command, string name, SqlDbType type, object value)
        {
            command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
        }

        Guid id;
        string correlationId;
        string replyToAddress;
        bool recoverable;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;
        MemoryStream bodyStream;

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));
    }
}