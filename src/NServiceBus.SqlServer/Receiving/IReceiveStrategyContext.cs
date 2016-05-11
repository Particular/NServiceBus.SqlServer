namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading.Tasks;
    using Transports;

    interface IReceiveStrategyContext : IDisposable
    {
        /// <summary>
        /// Two-step construction (sync ctor + async Initialize) is necessary because TransactionScope cannot be created in an awaited method because Transaction.Current 
        /// does not propagate down the stack through await call site (it only propagates up).
        /// </summary>
        Task Initialize();
        Task<MessageReadResult> TryReceive(TableBasedQueue queue);
        Task DeadLetter(TableBasedQueue queue, MessageRow poisonMessage);
        TransportTransaction TransportTransaction { get; }
        void Rollback();
        void Commit();
    }
}