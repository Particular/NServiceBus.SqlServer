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
                body = body
            };

        public void PrepareSendCommand(DbCommand command)
        {
            command.AddParameter("Id", DbType.Guid, id);
            command.AddParameter("TimeToBeReceivedMs", DbType.Int32, timeToBeReceived);
            command.AddParameter("Headers", DbType.String, headers, -1);
            BodyParameter.Add(command, body);
        }

        static async Task<MessageRow> ReadRow(DbDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken)
        {
            var row = new MessageRow
            {
                id = await dataReader.GetFieldValueAsync<Guid>(0, cancellationToken).ConfigureAwait(false),
                expired = await dataReader.GetFieldValueAsync<int>(1, cancellationToken).ConfigureAwait(false) == 1,
                headers = await GetHeaders(dataReader, 2, cancellationToken).ConfigureAwait(false),
                body = await GetBody(dataReader, 3, isStreamSupported, cancellationToken).ConfigureAwait(false),
                rowVersion = await dataReader.GetFieldValueAsync<long>(4, cancellationToken).ConfigureAwait(false)
            };

            return row;
        }

        MessageReadResult TryParse()
        {
            try
            {
                return MessageReadResult.Success(new Message(id.ToString(), headers, body, expired), rowVersion);
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return MessageReadResult.Poison(this, rowVersion);
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

        internal static Task<byte[]> ReadToEnd(Stream stream, CancellationToken cancellationToken = default) =>
            ReadToEnd(stream, ArrayPool<byte>.Shared, cancellationToken);

        internal static async Task<byte[]> ReadToEnd(Stream stream, ArrayPool<byte> pool, CancellationToken cancellationToken = default)
        {
            var segment = pool.Rent(SegmentSize);
            var segmentLength = 0;
            long length = 0;
            List<byte[]> filledSegments = null;
            var completed = false;
            try
            {
                while (true)
                {
                    if (segmentLength == segment.Length)
                    {
                        filledSegments ??= [];
                        var next = IsPooled(filledSegments.Count + 1) ? pool.Rent(SegmentSize) : new byte[SegmentSize];
                        filledSegments.Add(segment);
                        segment = next;
                        segmentLength = 0;
                    }

                    var read = await stream.ReadAsync(segment.AsMemory(segmentLength), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    segmentLength += read;
                    length += read;
                    if (length > Array.MaxLength)
                    {
                        throw new IOException($"The message body is larger than the maximum supported size of {Array.MaxLength} bytes.");
                    }
                }

                var body = GC.AllocateUninitializedArray<byte>((int)length);
                var offset = 0;
                if (filledSegments is not null)
                {
                    foreach (var filled in filledSegments)
                    {
                        filled.CopyTo(body, offset);
                        offset += filled.Length;
                    }
                }

                segment.AsSpan(0, segmentLength).CopyTo(body.AsSpan(offset));
                completed = true;
                return body;
            }
            finally
            {
                var segmentIndex = 0;
                if (filledSegments is not null)
                {
                    foreach (var filled in filledSegments)
                    {
                        if (IsPooled(segmentIndex++))
                        {
                            ReturnCleared(pool, filled, filled.Length);
                        }
                    }
                }

                if (IsPooled(segmentIndex))
                {
                    // A read that throws may already have written past segmentLength.
                    ReturnCleared(pool, segment, completed ? segmentLength : segment.Length);
                }
            }
        }

        // Segments stay below the LOH threshold and only the first MiB is pooled, so a large body
        // can't leave large or many arrays referenced by the process-wide pool.
        static bool IsPooled(int segmentIndex) => segmentIndex < PooledSegmentCount;

        // Clears only the used part because message bodies may contain sensitive data and the pool is process-wide.
        static void ReturnCleared(ArrayPool<byte> pool, byte[] buffer, int length)
        {
            buffer.AsSpan(0, length).Clear();
            pool.Return(buffer);
        }

        const int SegmentSize = 64 * 1024;
        const int PooledSegmentCount = 16;

        Guid id;
        bool expired;
        int? timeToBeReceived;
        string headers;
        ReadOnlyMemory<byte> body;
        long rowVersion;

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));
    }
}
