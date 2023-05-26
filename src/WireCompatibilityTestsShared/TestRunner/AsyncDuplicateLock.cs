using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;

public sealed class AsyncDuplicateLock
{
    sealed class RefCounted<T>
    {
        public RefCounted(T value)
        {
            RefCount = 1;
            Value = value;
        }

        public int RefCount { get; set; }
        public T Value { get; private set; }
    }

    static readonly Dictionary<object, RefCounted<SemaphoreSlim>> SemaphoreSlims = new Dictionary<object, RefCounted<SemaphoreSlim>>();

    SemaphoreSlim GetOrCreate(object key)
    {
        RefCounted<SemaphoreSlim> item;
        lock (SemaphoreSlims)
        {
            if (SemaphoreSlims.TryGetValue(key, out item))
            {
                ++item.RefCount;
            }
            else
            {
                item = new RefCounted<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                SemaphoreSlims[key] = item;
            }
        }
        return item.Value;
    }

    public IDisposable Lock(object key)
    {
        GetOrCreate(key).Wait();
        return new Releaser { Key = key };
    }

    public async Task<IDisposable> LockAsync(object key, CancellationToken cancellationToken = default)
    {
        await GetOrCreate(key).WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser { Key = key };
    }

    sealed class Releaser : IDisposable
    {
        public object Key { get; set; }

        public void Dispose()
        {
            RefCounted<SemaphoreSlim> item;
            lock (SemaphoreSlims)
            {
                item = SemaphoreSlims[Key];
                --item.RefCount;
                if (item.RefCount == 0)
                {
                    SemaphoreSlims.Remove(Key);
                }
            }
            item.Value.Release();
        }
    }
}