namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;

static partial class BodyParameter
{
    // Npgsql writes ReadOnlyMemory<byte> to bytea without copying it.
    private static partial void SetValue(DbParameter parameter, ReadOnlyMemory<byte> body, bool isStreamSupported) => parameter.Value = body;
}
