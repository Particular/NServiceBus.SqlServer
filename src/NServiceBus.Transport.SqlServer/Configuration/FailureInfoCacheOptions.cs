namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Options for the in-memory cache that tracks messages which failed processing and are waiting to be retried.
    /// </summary>
    /// <remarks>
    /// When a message fails, the transport rolls back and keeps the exception, the number of attempts, and the message
    /// context in memory. The recoverability policy runs the next time the message is received, so the retry decision and
    /// the move to the error queue are atomic with removing the message from the queue. The entry is removed once the
    /// message is processed successfully or handled by recoverability. Each receiver has its own cache.
    /// The defaults suit most systems. Change them when the retained exceptions hold a lot of memory, for example a
    /// <c>DbUpdateException</c>, which keeps the entities tracked by its <c>DbContext</c> reachable until the entry is removed.
    /// </remarks>
    public class FailureInfoCacheOptions
    {
        internal FailureInfoCacheOptions() { }

        /// <summary>
        /// The maximum number of failed messages tracked per receiver. When the limit is reached, the least recently used
        /// entries are evicted. The default is 10,000 and the minimum is 3.
        /// </summary>
        /// <remarks>
        /// Lower the value to bound the memory used by retained exceptions. Memory use is roughly the size of the largest
        /// exception graph multiplied by the number of entries.
        /// A failed message keeps its place in the queue and is normally received again right away, so the entries still
        /// needed are about the maximum concurrency of the endpoint. Keep the value comfortably above that concurrency,
        /// for example several times higher. An evicted message starts again with a single recorded attempt, so its
        /// immediate and delayed retries can restart and it can take longer to reach the error queue.
        /// </remarks>
        public int MaxEntries
        {
            get;
            set
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value, MinimumEntries);
                field = value;
            }
        } = DefaultMaxEntries;

        /// <summary>
        /// The time after which an entry is removed when the message has not failed again. The default is <c>null</c>,
        /// which means entries are only removed when the cache is full.
        /// </summary>
        /// <remarks>
        /// Set a value to release memory after a burst of failures. When the endpoint runs multiple instances, a retry
        /// can be received by another instance. The entry of the instance that recorded the failure then stays in its cache
        /// until it is evicted, as it does for messages that were removed from the queue. The time counts from the most
        /// recent failure of the message. While the receiver runs, expired entries are removed in the background at an
        /// interval equal to the expiration period, limited to between 1 second and 1 minute.
        /// Use a value that is well above the longest time between two attempts of the same message, including the
        /// time the endpoint can be paused by the circuit breaker (see <c>TimeToWaitBeforeTriggeringCircuitBreaker</c>).
        /// An expired entry resets the number of attempts the next time the message is received.
        /// </remarks>
        public TimeSpan? ExpirationPeriod
        {
            get;
            set
            {
                if (value.HasValue)
                {
                    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.Value, TimeSpan.Zero);
                }

                field = value;
            }
        }

        internal const int DefaultMaxEntries = 10000;
        const int MinimumEntries = 3;
    }
}
