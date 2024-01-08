namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
    using Npgsql;
    using NpgsqlTypes;

    class StoreDelayedMessageCommand
    {
        StoreDelayedMessageCommand() { }

        public static StoreDelayedMessageCommand From(Dictionary<string, string> headers, ReadOnlyMemory<byte> body, TimeSpan dueAfter, string destination)
        {
            Guard.AgainstNull(nameof(destination), destination);

            var row = new StoreDelayedMessageCommand();

            headers["NServiceBus.SqlServer.ForwardDestination"] = destination;
            row.headers = DictionarySerializer.Serialize(headers);
            row.bodyBytes = body.ToArray();
            row.dueAfter = dueAfter;
            return row;
        }


        public void PrepareSendCommand(NpgsqlCommand command)
        {
            AddParameter(command, "Headers", NpgsqlDbType.Varchar, headers);
            AddParameter(command, "Body", NpgsqlDbType.Bytea, bodyBytes);
            AddParameter(command, "DueAfterDays", NpgsqlDbType.Integer, dueAfter.Days);
            AddParameter(command, "DueAfterHours", NpgsqlDbType.Integer, dueAfter.Hours);
            AddParameter(command, "DueAfterMinutes", NpgsqlDbType.Integer, dueAfter.Minutes);
            AddParameter(command, "DueAfterSeconds", NpgsqlDbType.Integer, dueAfter.Seconds);
            AddParameter(command, "DueAfterMilliseconds", NpgsqlDbType.Integer, dueAfter.Milliseconds);
        }

        void AddParameter(NpgsqlCommand command, string name, NpgsqlDbType type, object value)
        {
            command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
        }

        string headers;
        byte[] bodyBytes;
        TimeSpan dueAfter;
    }
}