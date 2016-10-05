namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;

    class DelayedMessageRow
    {
        DelayedMessageRow() { }

        public static DelayedMessageRow From(Dictionary<string, string> headers, byte[] body, DateTime due, string destination)
        {
            var row = new DelayedMessageRow();

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
            row.due = due;
            row.destination = destination;
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
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes);
            AddParameter(command, "Due", SqlDbType.DateTime, due);
            AddParameter(command, "Destination", SqlDbType.VarChar, destination);
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
        DateTime due;
        string destination;
    }
}