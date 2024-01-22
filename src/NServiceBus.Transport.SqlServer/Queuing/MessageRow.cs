namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;

    class MessageRow
    {
        MessageRow() { }

        public static async Task<MessageReadResult> Read(DbDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken = default)
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

        public void PrepareSendCommand(DbCommand command)
        {
            command.AddParameter("Id", DbType.Guid, id);
            command.AddParameter("TimeToBeReceivedMs", DbType.Int32, timeToBeReceived);
            command.AddParameter("Headers", DbType.String, headers);
            command.AddParameter("Body", DbType.Binary, bodyBytes, -1);
        }

        static async Task<MessageRow> ReadRow(DbDataReader dataReader, bool isStreamSupported, CancellationToken cancellationToken)
        {
            return new MessageRow
            {
                id = await dataReader.GetFieldValueAsync<Guid>(0, cancellationToken).ConfigureAwait(false),
                expired = await dataReader.GetFieldValueAsync<int>(1, cancellationToken).ConfigureAwait(false) == 1,
                headers = await GetHeaders(dataReader, 2, cancellationToken).ConfigureAwait(false),
                bodyBytes = await GetBody(dataReader, 3, isStreamSupported, cancellationToken).ConfigureAwait(false)
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

        static async Task<string> GetHeaders(DbDataReader dataReader, int headersIndex, CancellationToken cancellationToken)
        {
            if (await dataReader.IsDBNullAsync(headersIndex, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            using (var textReader = dataReader.GetTextReader(headersIndex))
            {
                return await textReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        static async Task<byte[]> GetBody(DbDataReader dataReader, int bodyIndex, bool isStreamSupported, CancellationToken cancellationToken)
        {
            if (!isStreamSupported)
            {
                return (byte[])dataReader[bodyIndex];
            }

            // Null values will be returned as an empty (zero bytes) Stream.
            using (var outStream = new MemoryStream())
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                // 81920 is the default buffer size. Overload accepting (Stream, CancellationToken) not available in .NET Framework.
                await stream.CopyToAsync(outStream, 81920, cancellationToken).ConfigureAwait(false);
                return outStream.ToArray();
            }
        }

        Guid id;
        bool expired;
        int? timeToBeReceived;
        string headers;
        byte[] bodyBytes;

        static ILog Logger = LogManager.GetLogger(typeof(MessageRow));
    }
}