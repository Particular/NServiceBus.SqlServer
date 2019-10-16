namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading.Tasks;

    static class TaskEx
    {
        // ReSharper disable once UnusedParameter.Global
        // Used to explicitly suppress the compiler warning about
        // using the returned value from async operations
        public static void Ignore(this Task task)
        {
        }

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

        //TODO: remove when we update to 4.6 and can use Task.CompletedTask
        public static readonly Task CompletedTask = Task.FromResult(0);

        public static readonly Task<bool> TrueTask = Task.FromResult(true);
        public static readonly Task<bool> FalseTask = Task.FromResult(false);
    }
}