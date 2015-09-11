namespace NServiceBus.Transports.SQLServer
{
    using System;

    interface IBackOffStrategy
    {
        void ConditionalWait(Func<bool> condition, Action<int> waitAction);
    }
}