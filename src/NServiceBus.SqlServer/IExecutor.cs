namespace NServiceBus.Transports.SQLServer
{
    using System.Threading;

    interface IExecutor
    {
        void Start(int maximumConcurrency, CancellationToken token);
        void Stop();
    }

    class NullExecutor : IExecutor
    {
        public void Start(int maximumConcurrency, CancellationToken token)
        {
        }

        public void Stop()
        {
        }
    }
}