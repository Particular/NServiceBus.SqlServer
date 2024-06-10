namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Linq;
    using System.Threading;
    using Microsoft.Data.SqlClient;

    static class ExceptionExtensions
    {
        public static string GetMessage(this Exception exception)
        {
            try
            {
                return exception.Message;
            }
            catch (Exception)
            {
                return $"Could not read Message from exception type '{exception.GetType()}'.";
            }
        }

        public static bool IsObjectAlreadyExists(this Exception ex) => ex is SqlException { Number: 2714 or 1913 };

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
