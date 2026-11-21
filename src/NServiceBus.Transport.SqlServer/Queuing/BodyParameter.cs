namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data.Common;
    using System.Runtime.InteropServices;
    using Microsoft.Data.SqlClient;

    static partial class BodyParameter
    {
        private static partial void SetValue(DbParameter parameter, ReadOnlyMemory<byte> body, bool isStreamSupported)
        {
            if (MemoryMarshal.TryGetArray(body, out var segment))
            {
                if (segment.Offset == 0 && segment.Count == segment.Array.Length)
                {
                    parameter.Value = segment.Array;
                    return;
                }

                // Up to this size SqlClient declares varbinary(Size), which caches one plan per distinct body length.
                // Column encryption keeps the copy because Offset has not been verified with encrypted parameters.
                if (isStreamSupported && segment.Count > MaxNonPlpBinaryLength)
                {
                    var sqlParameter = (SqlParameter)parameter;
                    sqlParameter.Value = segment.Array;
                    sqlParameter.Offset = segment.Offset;
                    sqlParameter.Size = segment.Count;
                    return;
                }
            }

            parameter.Value = body.ToArray();
        }

        const int MaxNonPlpBinaryLength = 8000;
    }
}
