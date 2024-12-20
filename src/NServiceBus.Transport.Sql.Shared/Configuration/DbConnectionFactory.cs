namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

abstract class DbConnectionFactory
{
    protected DbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory) => openNewConnection = factory;

    protected DbConnectionFactory()
    {
    }

    public Task<DbConnection> OpenNewConnection(CancellationToken cancellationToken = default) => openNewConnection(cancellationToken);

    protected Func<CancellationToken, Task<DbConnection>> openNewConnection;
}