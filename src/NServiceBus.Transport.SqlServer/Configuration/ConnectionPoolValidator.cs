namespace NServiceBus.Transport.SqlServer;

using System;
using System.Data.Common;
using NServiceBus.Transport.Sql.Shared;

static class ConnectionPoolValidator
{
    public static ValidationCheckResult Validate(string connectionString)
    {
        var keys = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var hasPoolingValue = keys.TryGetValue("Pooling", out object poolingValue);
        if (hasPoolingValue && !string.Equals(poolingValue.ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
        {
            return ValidationCheckResult.Valid();
        }
        if (keys.ContainsKey("Max Pool Size"))
        {
            return ValidationCheckResult.Valid();
        }
        return ValidationCheckResult.Invalid(ConnectionPoolSizeNotSet);
    }

    const string ConnectionPoolSizeNotSet =
        "Maximum connection pooling value (Max Pool Size=N) is not " +
        "configured on the provided connection string. The default value (100) will be used.";
}