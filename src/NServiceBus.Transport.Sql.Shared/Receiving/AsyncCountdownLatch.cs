namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Threading;
using System.Threading.Tasks;

class AsyncCountdownLatch
{
    int count;
    readonly TaskCompletionSource completionSource;

    public AsyncCountdownLatch(int count)
    {
        this.count = count;
        completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (count <= 0)
        {
            completionSource.SetResult();
        }
    }

#pragma warning disable PS0003
    public Task WaitAsync(CancellationToken cancellationToken)
#pragma warning restore PS0003
    {
        _ = cancellationToken.Register(completionSource.SetResult);
        return completionSource.Task;
    }

    public Signaler GetSignaler() => new(this);

    void Signal()
    {
        if (Interlocked.Decrement(ref count) == 0)
        {
            completionSource.SetResult();
        }
    }

    public class Signaler(AsyncCountdownLatch parent) : IDisposable
    {
        bool signalled;

        public void Signal()
        {
            parent.Signal();
            signalled = true;
        }

        public void Dispose()
        {
            if (!signalled)
            {
                parent.Signal();
                signalled = true;
            }
        }
    }
}