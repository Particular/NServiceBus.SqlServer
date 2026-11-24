namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;

static partial class BodyParameter
{
    // Npgsql writes ReadOnlyMemory<byte> to bytea without copying it.
    private static partial void SetValue(DbCommand command, DbParameter parameter, ReadOnlyMemory<byte> body) => parameter.Value = body;
}
