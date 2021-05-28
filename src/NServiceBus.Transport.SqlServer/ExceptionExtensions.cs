namespace NServiceBus
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Linq;
    using System.Threading;

    static class ExceptionExtensions
    {
        // the SQL client sometimes throws an SqlException when operations are canceled instead of an OperationCanceledException
#pragma warning disable PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
        public static bool IsCausedBy(this Exception ex, CancellationToken cancellationToken) =>
            (ex is OperationCanceledException || (ex as SqlException).IsCausedByCancellation()) && cancellationToken.IsCancellationRequested;
#pragma warning restore PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional

        static bool IsCausedByCancellation(this SqlException ex) => ex != null && ex.Errors.OfType<SqlError>().Any(error => error.IsCausedByCancellation());

        // see https://github.com/dotnet/SqlClient/issues/26#issuecomment-723307003
        static bool IsCausedByCancellation(this SqlError error) => error.Number == 0 && error.State == 0 && error.Class == 11;
    }
}
