namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data;
    using System.Data.Common;

    // Each provider assembly implements SetValue because the cheapest way to send a body slice differs per ADO.NET provider.
    static partial class BodyParameter
    {
        public static void Add(DbCommand command, ReadOnlyMemory<byte> body, bool isStreamSupported)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "Body";
            parameter.DbType = DbType.Binary;
            parameter.Size = -1;
            SetValue(parameter, body, isStreamSupported);
            command.Parameters.Add(parameter);
        }

        private static partial void SetValue(DbParameter parameter, ReadOnlyMemory<byte> body, bool isStreamSupported);
    }
}
