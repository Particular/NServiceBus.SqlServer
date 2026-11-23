namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Runtime.ExceptionServices;
    using BitFaster.Caching;
    using BitFaster.Caching.Lru;
    using Extensibility;

    class FailureInfoStorage(int maxElements, TimeSpan? expireAfter = null)
    {
        public void RecordFailureInfoForMessage(string messageId, Exception exception, ContextBag context)
        {
            // A message is processed by one receiver at a time, so the read-modify-write needs no lock.
            var numberOfProcessingAttempts = failureInfoPerMessage.TryGet(messageId, out var existing)
                ? existing.NumberOfProcessingAttempts + 1
                : 1;

            failureInfoPerMessage.AddOrUpdate(messageId, new ProcessingFailureInfo(numberOfProcessingAttempts, ExceptionDispatchInfo.Capture(exception), context));
        }

        public bool TryGetFailureInfoForMessage(string messageId, out ProcessingFailureInfo processingFailureInfo) =>
            failureInfoPerMessage.TryGet(messageId, out processingFailureInfo);

        public void ClearFailureInfoForMessage(string messageId)
        {
            // Probing first keeps the usual miss lock-free; TryRemove takes a bucket lock even when the key is absent.
            if (failureInfoPerMessage.TryGet(messageId, out _))
            {
                failureInfoPerMessage.TryRemove(messageId);
            }
        }

        readonly ICache<string, ProcessingFailureInfo> failureInfoPerMessage = expireAfter is { } expiry
                ? new ConcurrentLruBuilder<string, ProcessingFailureInfo>().WithCapacity(maxElements).WithExpireAfterWrite(expiry).Build()
                : new FastConcurrentLru<string, ProcessingFailureInfo>(maxElements);

        public class ProcessingFailureInfo(int numberOfProcessingAttempts, ExceptionDispatchInfo exceptionDispatchInfo, ContextBag context)
        {
            public int NumberOfProcessingAttempts { get; } = numberOfProcessingAttempts;
            public Exception Exception => ExceptionDispatchInfo.SourceException;
            ExceptionDispatchInfo ExceptionDispatchInfo { get; } = exceptionDispatchInfo;
            public ContextBag Context { get; } = context;
        }
    }
}
