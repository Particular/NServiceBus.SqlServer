namespace NServiceBus.Transport.Sql.Shared.Configuration;

using System.Data.Common;
using System.Threading.Tasks;
using System.Threading;
using System;
using NServiceBus.Logging;

public abstract class DbConnectionFactory
{
    public DbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory, Func<string, ValidationCheckResult> connectionPoolValidator)
    {
        openNewConnection = factory;
        validateConnectionPool = connectionPoolValidator;
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

    protected void ValidateConnectionPool(string connectionString)
    {
        if (hasValidated)
        {
            return;
        }

        var validationResult = validateConnectionPool(connectionString);
        if (!validationResult.IsValid)
        {
            Logger.Warn(validationResult.Message);
        }

        hasValidated = true;
    }

    static bool hasValidated;

    protected Func<CancellationToken, Task<DbConnection>> openNewConnection;
    protected Func<string, ValidationCheckResult> validateConnectionPool;

    static ILog Logger = LogManager.GetLogger<DbConnectionFactory>();
}