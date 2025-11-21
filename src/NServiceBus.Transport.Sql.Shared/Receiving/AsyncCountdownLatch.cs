namespace NServiceBus.Transport.Sql.Shared;

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

    public Task WaitAsync() => completionSource.Task;

    public void Signal()
    {
        if (Interlocked.Decrement(ref count) == 0)
        {
            completionSource.SetResult();
        }
    }
}