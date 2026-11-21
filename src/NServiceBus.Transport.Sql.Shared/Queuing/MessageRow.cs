namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class MessageRow
    {
        MessageRow() { }

        public static async Task<MessageReadResult> Read(DbDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken = default)
        {
            var row = await ReadRow(dataReader, isStreamSupported, cancellationToken).ConfigureAwait(false);
            return row.TryParse();
        }

        public static MessageRow From(Dictionary<string, string> headers, ReadOnlyMemory<byte> body, TimeSpan toBeReceived) =>
            new()
            {
                id = Guid.NewGuid(),
                timeToBeReceived = toBeReceived == TimeSpan.MaxValue ? null : (int?)toBeReceived.TotalMilliseconds,
                headers = DictionarySerializer.Serialize(headers),
                bodyBytes = body.ToArray()
            };

        public void PrepareSendCommand(DbCommand command)
        {
            command.AddParameter("Id", DbType.Guid, id);
            command.AddParameter("TimeToBeReceivedMs", DbType.Int32, timeToBeReceived);
            command.AddParameter("Headers", DbType.String, headers, -1);
            command.AddParameter("Body", DbType.Binary, bodyBytes, -1);
        }

        static async Task<MessageRow> ReadRow(DbDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken) =>
            new()
            {
                id = await dataReader.GetFieldValueAsync<Guid>(0, cancellationToken).ConfigureAwait(false),
                expired = await dataReader.GetFieldValueAsync<int>(1, cancellationToken).ConfigureAwait(false) == 1,
                headers = await GetHeaders(dataReader, 2, cancellationToken).ConfigureAwait(false),
                bodyBytes = await GetBody(dataReader, 3, isStreamSupported, cancellationToken).ConfigureAwait(false)
            };

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

        static async Task<string> GetHeaders(DbDataReader dataReader, int headersIndex, CancellationToken cancellationToken)
        {
            if (await dataReader.IsDBNullAsync(headersIndex, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            using var textReader = dataReader.GetTextReader(headersIndex);
            return await textReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        static async Task<byte[]> GetBody(DbDataReader dataReader, int bodyIndex, bool isStreamSupported, CancellationToken cancellationToken)
        {
            if (!isStreamSupported)
            {
                return (byte[])dataReader[bodyIndex];
            }

            // Null values will be returned as an empty (zero bytes) Stream.
            var stream = dataReader.GetStream(bodyIndex);
            await using var streamDisposer = stream.ConfigureAwait(false);
            return await ReadToEnd(stream, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<byte[]> ReadToEnd(Stream stream, CancellationToken cancellationToken = default)
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(InitialBodyBufferSize);
            var length = 0;
            try
            {
                while (true)
                {
                    if (length == buffer.Length)
                    {
                        var larger = pool.Rent(buffer.Length * 2);
                        buffer.AsSpan(0, length).CopyTo(larger);
                        ReturnCleared(pool, buffer, length);
                        buffer = larger;
                    }

                    var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    length += read;
                }

                var body = GC.AllocateUninitializedArray<byte>(length);
                buffer.AsSpan(0, length).CopyTo(body);
                return body;
            }
            finally
            {
                ReturnCleared(pool, buffer, length);
            }
        }

        // Clears only the used part because message bodies may contain sensitive data and the pool is process-wide.
        static void ReturnCleared(ArrayPool<byte> pool, byte[] buffer, int length)
        {
            buffer.AsSpan(0, length).Clear();
            pool.Return(buffer);
        }

        const int InitialBodyBufferSize = 16 * 1024;

        Guid id;
        bool expired;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));
    }
}
