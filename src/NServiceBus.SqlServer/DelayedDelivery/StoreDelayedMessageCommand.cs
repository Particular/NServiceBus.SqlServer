namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;

    class StoreDelayedMessageCommand
    {
        StoreDelayedMessageCommand()
        {
        }

        public static StoreDelayedMessageCommand From(Dictionary<string, string> headers, byte[] body, TimeSpan dueAfter, string destination)
        {
            Guard.AgainstNull(nameof(destination), destination);

            var row = new StoreDelayedMessageCommand();

            headers["NServiceBus.SqlServer.ForwardDestination"] = destination;
            row.headers = DictionarySerializer.Serialize(headers);
            row.bodyBytes = body;
            row.dueAfter = dueAfter;
            return row;
        }


        public void PrepareSendCommand(DbCommand command)
        {
            AddParameter(command, "Headers", DbType.String, headers);
            AddParameter(command, "Body", DbType.Binary, bodyBytes);
            AddParameter(command, "DueAfterDays", DbType.Int32, dueAfter.Days);
            AddParameter(command, "DueAfterHours", DbType.Int32, dueAfter.Hours);
            AddParameter(command, "DueAfterMinutes", DbType.Int32, dueAfter.Minutes);
            AddParameter(command, "DueAfterSeconds", DbType.Int32, dueAfter.Seconds);
            AddParameter(command, "DueAfterMilliseconds", DbType.Int32, dueAfter.Milliseconds);
        }

        static void AddParameter(DbCommand command, string name, DbType type, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        string headers;
        byte[] bodyBytes;
        TimeSpan dueAfter;
    }
}