namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Threading;
using Sql.Shared;

class PostgreSqlExceptionClassifier : IExceptionClassifier
{
    //TODO: Check if npgsql does not throw other exceptions that should be treated as OCE
#pragma warning disable PS0003
    public bool IsOperationCancelled(Exception exception, CancellationToken cancellationToken) => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
#pragma warning restore PS0003

    //TODO: Check how to find out if an exception is a deadlock
    public bool IsDeadlockException(Exception ex) => false;
}