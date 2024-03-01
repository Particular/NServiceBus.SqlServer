namespace NServiceBus.Transport.SqlServer;

using System;
using System.Threading;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using Sql.Shared;

class SqlServerExceptionClassifier : IExceptionClassifier
{
#pragma warning disable PS0003
    public bool IsOperationCancelled(Exception exception, CancellationToken cancellationToken) => exception.IsCausedBy(cancellationToken);
    public bool IsDeadlockException(Exception ex) => ex is SqlException { Number: 1205 };
#pragma warning restore PS0003
}