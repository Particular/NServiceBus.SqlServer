namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;

    class MessageRow
    {
        public Guid Id { get; }
        public string CorrelationId { get; }
        public string ReplyToAddress { get; }
        public bool Recoverable { get; }
        public int? TimeToBeReceived { get; }
        public string Headers { get; }
        public byte[] Body { get; }

        public MessageRow(Guid id, string correlationId, string replyToAddress, bool recoverable, int? timeToBeReceived, string headers, byte[] body)
        {
            Id = id;
            CorrelationId = correlationId;
            ReplyToAddress = replyToAddress;
            Recoverable = recoverable;
            TimeToBeReceived = timeToBeReceived;
            Headers = headers;
            Body = body;
        }

        // We are assuming sequential access, order is important
        internal static async Task<MessageRow> ParseRawData(SqlDataReader dataReader)
        {
            var transportId = await dataReader.GetFieldValueAsync<Guid>(Sql.Columns.Id.Index).ConfigureAwait(false);

            var correlationId = await GetNullableValueAsync<string>(dataReader, Sql.Columns.CorrelationId.Index).ConfigureAwait(false);

            var replyToAddress = await GetNullableValueAsync<string>(dataReader, Sql.Columns.ReplyToAddress.Index).ConfigureAwait(false);

            var recoverable = await dataReader.GetFieldValueAsync<bool>(Sql.Columns.Recoverable.Index).ConfigureAwait(false);

            int? millisecondsToExpiry = null;
            if (!await dataReader.IsDBNullAsync(Sql.Columns.TimeToBeReceived.Index).ConfigureAwait(false))
            {
                millisecondsToExpiry = await dataReader.GetFieldValueAsync<int>(Sql.Columns.TimeToBeReceived.Index).ConfigureAwait(false);
            }

            var headers = await GetHeaders(dataReader, Sql.Columns.Headers.Index).ConfigureAwait(false);

            var bodyStream = await GetBody(dataReader, Sql.Columns.Body.Index).ConfigureAwait(false);

            var message = new MessageRow(transportId, correlationId, replyToAddress, recoverable, millisecondsToExpiry, headers, bodyStream);


            return message;
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

        static async Task<byte[]> GetBody(SqlDataReader dataReader, int bodyIndex)
        {
            var memoryStream = new MemoryStream();
            // Null values will be returned as an empty (zero bytes) Stream.
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            }
            return memoryStream.GetBuffer();
        }        

        public void PrepareSendCommand(SqlCommand command)
        {
            command.Parameters.Add(Sql.Columns.Id.Name, SqlDbType.UniqueIdentifier).Value = Id;
            command.Parameters.Add(Sql.Columns.CorrelationId.Name, SqlDbType.VarChar).Value = Nullable(CorrelationId);
            command.Parameters.Add(Sql.Columns.ReplyToAddress.Name, SqlDbType.VarChar).Value = Nullable(ReplyToAddress);
            command.Parameters.Add(Sql.Columns.Recoverable.Name, SqlDbType.Bit).Value = Recoverable;
            command.Parameters.Add(Sql.Columns.TimeToBeReceived.Name, SqlDbType.Int).Value = Nullable(TimeToBeReceived);
            command.Parameters.Add(Sql.Columns.Headers.Name, SqlDbType.VarChar).Value = Headers;
            command.Parameters.Add(Sql.Columns.Body.Name, SqlDbType.VarBinary).Value = Body;
        }

        static object Nullable(object value)
        {
            return value ?? DBNull.Value;
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