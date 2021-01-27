namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Threading.Tasks;

    static class TaskEx
    {
        public static async Task IgnoreCancellation(this Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}