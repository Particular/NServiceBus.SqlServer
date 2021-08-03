namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.IO;
    using System.Threading.Tasks;
    using Logging;
    using System.Threading;

    class MessageRow
    {
        MessageRow() { }

        public static async Task<MessageReadResult> Read(SqlDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken = default)
        {
            var row = await ReadRow(dataReader, isStreamSupported, cancellationToken).ConfigureAwait(false);
            return row.TryParse();
        }

        public static MessageRow From(Dictionary<string, string> headers, ReadOnlyMemory<byte> body, TimeSpan toBeReceived)
        {
            return new MessageRow
            {
                id = Guid.NewGuid(),
                timeToBeReceived = toBeReceived == TimeSpan.MaxValue ? null : (int?)toBeReceived.TotalMilliseconds,
                headers = DictionarySerializer.Serialize(headers),
                bodyBytes = body.ToArray()
            };
        }


        public void PrepareSendCommand(SqlCommand command)
        {
            AddParameter(command, "Id", SqlDbType.UniqueIdentifier, id);
            AddParameter(command, "TimeToBeReceivedMs", SqlDbType.Int, timeToBeReceived);
            AddParameter(command, "Headers", SqlDbType.NVarChar, headers);
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes, -1);
        }

        static async Task<MessageRow> ReadRow(SqlDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken)
        {
            return new MessageRow
            {
                id = await dataReader.GetFieldValueAsync<Guid>(0, cancellationToken).ConfigureAwait(false),
                expired = await dataReader.GetFieldValueAsync<int>(1, cancellationToken).ConfigureAwait(false) == 1,
                headers = await GetHeaders(dataReader, 2, cancellationToken).ConfigureAwait(false),
                bodyBytes = isStreamSupported ? await GetBody(dataReader, 3, cancellationToken).ConfigureAwait(false) : await GetNonStreamBody(dataReader, 5, cancellationToken).ConfigureAwait(false)
            };
        }

        MessageReadResult TryParse()
        {
            try
            {
                return MessageReadResult.Success(new Message(id.ToString(), headers, bodyBytes, expired));
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return MessageReadResult.Poison(this);
            }
        }

        static async Task<string> GetHeaders(SqlDataReader dataReader, int headersIndex, CancellationToken cancellationToken)
        {
            if (await dataReader.IsDBNullAsync(headersIndex, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            using (var textReader = dataReader.GetTextReader(headersIndex))
            {
                return await textReader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        static async Task<byte[]> GetBody(SqlDataReader dataReader, int bodyIndex, CancellationToken cancellationToken)
        {
            // Null values will be returned as an empty (zero bytes) Stream.
            using (var outStream = new MemoryStream())
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                // 81920 is the default buffer size. Overload accepting (Stream, CancellationToken) not available in .NET Framework.
                await stream.CopyToAsync(outStream, 81920, cancellationToken).ConfigureAwait(false);
                return outStream.ToArray();
            }
        }

        static Task<byte[]> GetNonStreamBody(SqlDataReader dataReader, int bodyIndex, CancellationToken cancellationToken)
        {
            return Task.FromResult((byte[])dataReader[bodyIndex]);
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
        bool expired;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));
    }
}