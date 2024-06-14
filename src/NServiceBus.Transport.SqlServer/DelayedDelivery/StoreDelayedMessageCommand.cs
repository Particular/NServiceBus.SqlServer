namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Microsoft.Data.SqlClient;

    class StoreDelayedMessageCommand
    {
        StoreDelayedMessageCommand() { }

        public static StoreDelayedMessageCommand From(Dictionary<string, string> headers, ReadOnlyMemory<byte> body, TimeSpan dueAfter, string destination)
        {
            ArgumentNullException.ThrowIfNull(destination);

            var row = new StoreDelayedMessageCommand();

            headers["NServiceBus.SqlServer.ForwardDestination"] = destination;
            row.headers = DictionarySerializer.Serialize(headers);
            row.bodyBytes = body.ToArray();
            row.dueAfter = dueAfter;
            return row;
        }


        public void PrepareSendCommand(SqlCommand command)
        {
            AddParameter(command, "Headers", SqlDbType.NVarChar, headers);
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes);
            AddParameter(command, "DueAfterDays", SqlDbType.Int, dueAfter.Days);
            AddParameter(command, "DueAfterHours", SqlDbType.Int, dueAfter.Hours);
            AddParameter(command, "DueAfterMinutes", SqlDbType.Int, dueAfter.Minutes);
            AddParameter(command, "DueAfterSeconds", SqlDbType.Int, dueAfter.Seconds);
            AddParameter(command, "DueAfterMilliseconds", SqlDbType.Int, dueAfter.Milliseconds);
        }

        void AddParameter(SqlCommand command, string name, SqlDbType type, object value)
        {
            command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
        }

        string headers;
        byte[] bodyBytes;
        TimeSpan dueAfter;
    }
}