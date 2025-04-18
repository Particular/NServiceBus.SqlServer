﻿namespace NServiceBus.Transport.PostgreSql;

using System;
using System.Threading;
using Npgsql;
using NServiceBus.Transport.Sql.Shared;

class PostgreSqlExceptionClassifier : IExceptionClassifier
{
#pragma warning disable PS0003
    public bool IsOperationCancelled(Exception exception, CancellationToken cancellationToken) =>
        exception.IsCausedBy(cancellationToken);
#pragma warning restore PS0003

    public bool IsDeadlockException(Exception ex) => ex is NpgsqlException { SqlState: "40P01" };
}