namespace NServiceBus.Transport.Sql.Shared.DelayedDelivery
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using Queuing;

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


        public void PrepareSendCommand(DbCommand command)
        {
            command.AddParameter("Headers", DbType.String, headers);
            command.AddParameter("Body", DbType.Binary, bodyBytes);
            command.AddParameter("DueAfterDays", DbType.Int32, dueAfter.Days);
            command.AddParameter("DueAfterHours", DbType.Int32, dueAfter.Hours);
            command.AddParameter("DueAfterMinutes", DbType.Int32, dueAfter.Minutes);
            command.AddParameter("DueAfterSeconds", DbType.Int32, dueAfter.Seconds);
            command.AddParameter("DueAfterMilliseconds", DbType.Int32, dueAfter.Milliseconds);
        }

        string headers;
        byte[] bodyBytes;
        TimeSpan dueAfter;
    }
}