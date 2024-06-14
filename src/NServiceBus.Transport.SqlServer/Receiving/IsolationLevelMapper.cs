namespace NServiceBus.Transport.SqlServer
{
    using System.Data;

    static class IsolationLevelMapper
    {
        public static IsolationLevel Map(System.Transactions.IsolationLevel isolationLevel) => isolationLevel switch
        {
            System.Transactions.IsolationLevel.Serializable => IsolationLevel.Serializable,
            System.Transactions.IsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
            System.Transactions.IsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
            System.Transactions.IsolationLevel.ReadUncommitted => IsolationLevel.ReadUncommitted,
            System.Transactions.IsolationLevel.Snapshot => IsolationLevel.Snapshot,
            System.Transactions.IsolationLevel.Chaos => IsolationLevel.Chaos,
            System.Transactions.IsolationLevel.Unspecified => IsolationLevel.Unspecified,
            _ => IsolationLevel.ReadCommitted
        };
    }
}