namespace NServiceBus.Transport.Sql.Shared
{
    using System.Data.Common;

    interface IMoveDueDelayedMessagesCommand
    {
        void Populate(DbCommand command, int batchSize);
    }
}
