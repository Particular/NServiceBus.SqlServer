namespace NServiceBus.Transport.SqlServer;

using System;
using System.Threading;
using Microsoft.Data.SqlClient;
using NServiceBus.Transport.Sql.Shared;

class SqlServerExceptionClassifier : IExceptionClassifier
{
#pragma warning disable PS0003
    public bool IsOperationCancelled(Exception exception, CancellationToken cancellationToken) => exception.IsCausedBy(cancellationToken);
#pragma warning restore PS0003

    public bool IsDeadlockException(Exception ex) => ex is SqlException { Number: 1205 };
}