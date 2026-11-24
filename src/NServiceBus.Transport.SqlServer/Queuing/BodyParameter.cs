namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Runtime.InteropServices;
    using Microsoft.Data.SqlClient;

    static partial class BodyParameter
    {
        private static partial void SetValue(DbCommand command, DbParameter parameter, ReadOnlyMemory<byte> body)
        {
            if (MemoryMarshal.TryGetArray(body, out var segment))
            {
                if (segment.Offset == 0 && segment.Count == segment.Array.Length)
                {
                    parameter.Value = segment.Array;
                    return;
                }

                // Up to this size SqlClient declares varbinary(Size), which caches one plan per distinct body length.
                if (segment.Count > MaxNonPlpBinaryLength && !MayEncryptParameters(command))
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

        // Offset has not been verified with Always Encrypted. The connection can come from the user or a custom
        // connection factory, so the endpoint's connection string doesn't tell.
        static bool MayEncryptParameters(DbCommand command)
        {
            if (command is not SqlCommand sqlCommand)
            {
                return true;
            }

            return sqlCommand.ColumnEncryptionSetting switch
            {
                SqlCommandColumnEncryptionSetting.Disabled or SqlCommandColumnEncryptionSetting.ResultSetOnly => false,
                SqlCommandColumnEncryptionSetting.UseConnectionSetting => sqlCommand.Connection is null || ConnectionEnablesColumnEncryption(sqlCommand.Connection.ConnectionString),
                SqlCommandColumnEncryptionSetting.Enabled => true,
                _ => true
            };
        }

        static bool ConnectionEnablesColumnEncryption(string connectionString)
        {
            if (columnEncryptionByConnectionString.TryGetValue(connectionString, out var enabled))
            {
                return enabled;
            }

            enabled = new SqlConnectionStringBuilder(connectionString).ColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled;

            if (columnEncryptionByConnectionString.Count < MaxCachedConnectionStrings)
            {
                columnEncryptionByConnectionString.TryAdd(connectionString, enabled);
            }

            return enabled;
        }

        static readonly ConcurrentDictionary<string, bool> columnEncryptionByConnectionString = new();

        const int MaxNonPlpBinaryLength = 8000;
        const int MaxCachedConnectionStrings = 64;
    }
}
