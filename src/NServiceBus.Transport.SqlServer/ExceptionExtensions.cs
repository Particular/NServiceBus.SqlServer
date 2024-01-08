namespace NServiceBus
{
    using System;
    using System.Threading;

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

        // the SQL client sometimes throws an SqlException when operations are canceled instead of an OperationCanceledException
#pragma warning disable PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
        public static bool IsCausedBy(this Exception ex, CancellationToken cancellationToken) =>
            ex is OperationCanceledException;
#pragma warning restore PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
    }
}
