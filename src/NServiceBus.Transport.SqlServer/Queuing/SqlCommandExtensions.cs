namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;

    static class SqlCommandExtensions
    {
        public static async Task<TScalar> ExecuteScalarAsyncOrDefault<TScalar>(this SqlCommand command, string commandName, Action<string> onUnexpectedValueMessage, CancellationToken cancellationToken = default)
        {
            var obj = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (obj is null || Convert.IsDBNull(obj))
            {
                if (default(TScalar) != null)
                {
                    onUnexpectedValueMessage?.Invoke($"{commandName} returned a null value.");
                }

                return default;
            }

            if (obj is not TScalar scalar)
            {
                onUnexpectedValueMessage?.Invoke($"{commandName} returned an unexpected value of type {obj.GetType()}.");

                return default;
            }

            return scalar;
        }

        public static Task<TScalar> ExecuteScalarAsync<TScalar>(this SqlCommand command, string commandName, CancellationToken cancellationToken = default) =>
            command.ExecuteScalarAsyncOrDefault<TScalar>(commandName, msg => throw new Exception(msg), cancellationToken);
    }
}
