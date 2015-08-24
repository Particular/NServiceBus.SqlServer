namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Threading.Tasks;

    interface ITaskTracker
    {
        void StartAndTrack(Func<Tuple<Guid, Task>> taskFactory);
        void Forget(Guid id);
        bool ShouldStartAnotherTaskImmediately { get; }
        void ShutdownAll();
    }
}