namespace NServiceBus.Transports.SQLServer
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
        MessageRow()
        {
        }

        public MessageRow(OutgoingMessage message)
        {
            id = Guid.NewGuid();
            correlationId = TryGetHeaderValue(message.Headers, Headers.CorrelationId, s => s);
            replyToAddress = TryGetHeaderValue(message.Headers, Headers.ReplyToAddress, s => s);
            recoverable = true;
            timeToBeReceived = TryGetHeaderValue(message.Headers, Headers.TimeToBeReceived, s =>
            {
                TimeSpan ttbr;
                return TimeSpan.TryParse(s, out ttbr)
                    ? (int?) ttbr.TotalMilliseconds
                    : null;
            });
            headers = DictionarySerializer.Serialize(message.Headers);
            bodyBytes = message.Body;
        }

        /// <summary>
        /// Reads the values from the data reader.
        /// </summary>
        /// <remarks>
        /// We are assuming sequential access, order is important
        /// </remarks>
        public static async Task<MessageRow> Read(SqlDataReader dataReader)
        {
            var row = new MessageRow();
            for (var col = 0; col < columns.Length; col++)
            {
                await columns[col].Read(dataReader, row, col).ConfigureAwait(false);
            }
            return row;
        }

        public MessageReadResult TryParse()
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

                var expired = timeToBeReceived.HasValue && timeToBeReceived.Value < 0;
                if (expired)
                {
                    Logger.InfoFormat($"Message with ID={id} has expired. Removing it from queue.");
                    return MessageReadResult.NoMessage;
                }
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

        public void PrepareSendCommand(SqlCommand command)
        {
            foreach (var columnInfo in columns)
            {
                columnInfo.AddParameter(command, this);
            }
        }

        static async Task<T> GetNullableAsync<T>(SqlDataReader dataReader, int index)
            where T : class
        {
            if (await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return default(T);
            }
            return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
        }

        static async Task<T?> GetNullableValueAsync<T>(SqlDataReader dataReader, int index)
            where T : struct
        {
            if (await dataReader.IsDBNullAsync(index).ConfigureAwait(false))
            {
                return default(T);
            }
            return await dataReader.GetFieldValueAsync<T>(index).ConfigureAwait(false);
        }

        Guid id;
        string correlationId;
        string replyToAddress;
        bool recoverable;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;
        MemoryStream bodyStream;

        static readonly ColumnInfo[] columns =
        {
            new ColumnInfo("Id", SqlDbType.UniqueIdentifier, r => r.id, async (row, reader, column) => { row.id = await reader.GetFieldValueAsync<Guid>(column).ConfigureAwait(false); }),
            new ColumnInfo("CorrelationId", SqlDbType.VarChar, r => r.correlationId, async (row, reader, column) => { row.correlationId = await GetNullableAsync<string>(reader, column).ConfigureAwait(false); }),
            new ColumnInfo("ReplyToAddress", SqlDbType.VarChar, r => r.replyToAddress, async (row, reader, column) => { row.replyToAddress = await GetNullableAsync<string>(reader, column).ConfigureAwait(false); }),
            new ColumnInfo("Recoverable", SqlDbType.Bit, r => r.recoverable, async (row, reader, column) => { row.recoverable = await reader.GetFieldValueAsync<bool>(column).ConfigureAwait(false); }),
            new ColumnInfo("TimeToBeReceivedMs", SqlDbType.Int, r => r.timeToBeReceived, async (row, reader, column) => { row.timeToBeReceived = await GetNullableValueAsync<int>(reader, column).ConfigureAwait(false); }),
            new ColumnInfo("Headers", SqlDbType.VarChar, r => r.headers, async (row, reader, column) => { row.headers = await GetHeaders(reader, column).ConfigureAwait(false); }),
            new ColumnInfo("Body", SqlDbType.VarBinary, r => r.bodyBytes ?? r.bodyStream.ToArray(), async (row, reader, column) => { row.bodyStream = await GetBody(reader, column).ConfigureAwait(false); })
        };

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));

        class ColumnInfo
        {
            public ColumnInfo(string name, SqlDbType type, Func<MessageRow, object> getValue, Func<MessageRow, SqlDataReader, int, Task> setValue)
            {
                this.name = name;
                this.type = type;
                this.getValue = getValue;
                this.setValue = setValue;
            }

            public void AddParameter(SqlCommand command, MessageRow row)
            {
                command.Parameters.Add(name, type).Value = ReplaceNullWithDBNull(getValue(row));
            }

            public Task Read(SqlDataReader dataReader, MessageRow row, int column)
            {
                return setValue(row, dataReader, column);
            }

            static object ReplaceNullWithDBNull(object value)
            {
                return value ?? DBNull.Value;
            }

            string name;
            SqlDbType type;
            Func<MessageRow, object> getValue;
            Func<MessageRow, SqlDataReader, int, Task> setValue;
        }
    }
}