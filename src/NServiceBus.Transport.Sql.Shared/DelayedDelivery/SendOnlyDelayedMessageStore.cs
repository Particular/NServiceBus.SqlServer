namespace NServiceBus.Transport.Sql.Shared.DelayedDelivery;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

public class SendOnlyDelayedMessageStore : IDelayedMessageStore
{
    public Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection,
        DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        throw new Exception("Delayed delivery is not supported for send-only endpoints.");
    }
}