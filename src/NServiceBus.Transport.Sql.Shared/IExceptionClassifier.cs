namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Threading;

public interface IExceptionClassifier
{
#pragma warning disable PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
    bool IsOperationCancelled(Exception exception, CancellationToken cancellationToken);
#pragma warning restore PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional

    bool IsDeadlockException(Exception ex);

    bool ObjectAlreadyExists(Exception ex);

}