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

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        var registration = cancellationToken.Register(static state => ((TaskCompletionSource)state).TrySetResult(), completionSource);
        await using var _ = registration.ConfigureAwait(false);
        await completionSource.Task.ConfigureAwait(false);
    }

    public Signaler GetSignaler() => new(this);

    void Signal()
    {
        if (Interlocked.Decrement(ref count) == 0)
        {
            _ = completionSource.TrySetResult();
        }
    }

    public struct Signaler(AsyncCountdownLatch parent) : IDisposable
    {
        bool signalled;

        public void Signal()
        {
            if (signalled)
            {
                return;
            }

            parent.Signal();
            signalled = true;
        }

        public void Dispose()
        {
            if (signalled)
            {
                return;
            }

            parent.Signal();
            signalled = true;
        }
    }
}