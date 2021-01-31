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

        public static readonly Task<bool> TrueTask = Task.FromResult(true);
        public static readonly Task<bool> FalseTask = Task.FromResult(false);
    }
}