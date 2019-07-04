namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;

    class DelayedMessageRow
    {
        DelayedMessageRow() { }

        public static DelayedMessageRow From(Dictionary<string, string> headers, byte[] body, TimeSpan dueAfter, string destination)
        {
            Guard.AgainstNull(nameof(destination), destination);

            var row = new DelayedMessageRow();

            headers["NServiceBus.SqlServer.ForwardDestination"] = destination;
            row.headers = DictionarySerializer.Serialize(headers);
            row.bodyBytes = body;
            row.dueAfter = dueAfter;
            return row;
        }


        public void PrepareSendCommand(SqlCommand command)
        {
            AddParameter(command, "Headers", SqlDbType.NVarChar, headers);
            AddParameter(command, "Body", SqlDbType.VarBinary, bodyBytes);
            AddParameter(command, "DueAfterMs", SqlDbType.BigInt, dueAfter.TotalMilliseconds);
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