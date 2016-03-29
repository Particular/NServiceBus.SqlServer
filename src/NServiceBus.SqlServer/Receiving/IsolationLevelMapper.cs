namespace NServiceBus.Transports.SQLServer
{
    using System.Transactions;

    internal static class IsolationLevelMapper
    {
        public static System.Data.IsolationLevel Map(IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case IsolationLevel.Serializable:
                    return System.Data.IsolationLevel.Serializable;
                case IsolationLevel.RepeatableRead:
                    return System.Data.IsolationLevel.RepeatableRead;
                case IsolationLevel.ReadCommitted:
                    return System.Data.IsolationLevel.ReadCommitted;
                case IsolationLevel.ReadUncommitted:
                    return System.Data.IsolationLevel.ReadUncommitted;
                case IsolationLevel.Snapshot:
                    return System.Data.IsolationLevel.Snapshot;
                case IsolationLevel.Chaos:
                    return System.Data.IsolationLevel.Chaos;
                case IsolationLevel.Unspecified:
                    return System.Data.IsolationLevel.Unspecified;
            }

            return System.Data.IsolationLevel.ReadCommitted;
        }

    }
}