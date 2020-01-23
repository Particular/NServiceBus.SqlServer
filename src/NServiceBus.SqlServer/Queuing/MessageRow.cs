namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
#if !MSSQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
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
            return row.TryParse();
        }

        public static MessageRow From(Dictionary<string, string> headers, byte[] body, TimeSpan toBeReceived)
        {
            return new MessageRow
            {
                id = Guid.NewGuid(),
                correlationId = TryGetHeaderValue(headers, Headers.CorrelationId, s => s),
                replyToAddress = TryGetHeaderValue(headers, Headers.ReplyToAddress, s => s),
                timeToBeReceived = toBeReceived == TimeSpan.MaxValue ? null : (int?)toBeReceived.TotalMilliseconds,
                headers = DictionarySerializer.Serialize(headers),
                bodyBytes = body
            };
        }


        public void PrepareSendCommand(SqlCommand command)
        {
            AddParameter(command, "Id", SqlDbType.UniqueIdentifier, id);
            AddParameter(command, "CorrelationId", SqlDbType.VarChar, correlationId);
            AddParameter(command, "ReplyToAddress", SqlDbType.VarChar, replyToAddress);
            AddParameter(command, "TimeToBeReceivedMs", SqlDbType.Int, timeToBeReceived);
            AddParameter(command, "Headers", SqlDbType.NVarChar, headers);
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes, -1);
        }

        static async Task<MessageRow> ReadRow(SqlDataReader dataReader)
        {
            //HINT: we are assuming that dataReader is sequential. Order or reads is important !
            return new MessageRow
            {
                id = await dataReader.GetFieldValueAsync<Guid>(0).ConfigureAwait(false),
                correlationId = await GetNullableAsync<string>(dataReader, 1).ConfigureAwait(false),
                replyToAddress = await GetNullableAsync<string>(dataReader, 2).ConfigureAwait(false),
                expired = await dataReader.GetFieldValueAsync<int>(3).ConfigureAwait(false) == 1,
                headers = await GetHeaders(dataReader, 4).ConfigureAwait(false),
                bodyBytes = await GetBody(dataReader, 5).ConfigureAwait(false)
            };
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
                return MessageReadResult.Success(new Message(id.ToString(), parsedHeaders, bodyBytes, expired));
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return MessageReadResult.Poison(this);
            }
        }

        static T TryGetHeaderValue<T>(Dictionary<string, string> headers, string name, Func<string, T> conversion)
        {
            if (headers.TryGetValue(name, out var text))
            {
                return conversion(text);
            }
            return default(T);
        }

        static async Task<string> GetHeaders(SqlDataReader dataReader, int headersIndex)
        {
            if (await dataReader.IsDBNullAsync(headersIndex).ConfigureAwait(false))
            {
                return null;
            }

            using (var textReader = dataReader.GetTextReader(headersIndex))
            {
                return await textReader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        static async Task<byte[]> GetBody(SqlDataReader dataReader, int bodyIndex)
        {
            // Null values will be returned as an empty (zero bytes) Stream.
            using (var outStream = new MemoryStream())
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                await stream.CopyToAsync(outStream).ConfigureAwait(false);
                return outStream.ToArray();
            }
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

        void AddParameter(SqlCommand command, string name, SqlDbType type, object value, int size)
        {
            command.Parameters.Add(name, type, size).Value = value ?? DBNull.Value;
        }

        Guid id;
        string correlationId;
        string replyToAddress;
        bool expired;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));
    }
}