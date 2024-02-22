namespace NServiceBus.Transport.Sql.Shared.Configuration;

using System.Data.Common;
using System.Threading.Tasks;
using System.Threading;
using System;

public abstract class DbConnectionFactory
{
    public DbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory)
    {
        openNewConnection = factory;
    }

    protected DbConnectionFactory()
    {
    }

    public async Task<DbConnection> OpenNewConnection(CancellationToken cancellationToken = default)
    {
        var connection = await openNewConnection(cancellationToken).ConfigureAwait(false);

        ValidateConnectionPool(connection.ConnectionString);

        return connection;
    }

    protected abstract void ValidateConnectionPool(string connectionString);

    protected Func<CancellationToken, Task<DbConnection>> openNewConnection;
}