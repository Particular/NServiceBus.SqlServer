namespace NServiceBus.Transport.Sql.Shared
{
    using System.Data.Common;

    interface IMoveDueDelayedMessagesCommand
    {
        bool OwnsTransaction { get; }

        void Populate(DbCommand command, int batchSize);
    }
}
