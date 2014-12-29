namespace NServiceBus.Transports.SQLServer
{
    using System.Threading;

    interface IExecutor
    {
        void Start(int maximumConcurrency, CancellationTokenSource tokenSource);
        void Stop();
    }

    class NullExecutor : IExecutor
    {
        public void Start(int maximumConcurrency, CancellationTokenSource tokenSource)
        {
        }

        public void Stop()
        {
        }
    }
}