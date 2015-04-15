namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;

    interface ITaskTracker
    {
        void StartAndTrack(Func<Task> taskFactory);
        void Forget(Task receiveTask);
        bool ShouldStartAnotherTaskImmediately { get; }
        void ShutdownAll();
    }
}