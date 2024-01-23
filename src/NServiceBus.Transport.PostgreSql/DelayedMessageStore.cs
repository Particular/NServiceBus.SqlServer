namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SqlServer;

class DelayedMessageStore : IDelayedMessageStore
{
    public Task Store(OutgoingMessage message, TimeSpan dueAfter, string destination, DbConnection connection,
        DbTransaction transaction, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}