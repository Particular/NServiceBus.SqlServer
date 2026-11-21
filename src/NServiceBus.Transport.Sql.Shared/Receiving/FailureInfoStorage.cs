namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using Extensibility;

    // The data structure has fixed maximum size. When the data structure reaches its maximum size,
    // the least recently used (LRU) message processing failure is removed from the storage.
    class FailureInfoStorage(int maxElements)
    {
        public void RecordFailureInfoForMessage(string messageId, Exception exception, ContextBag context)
        {
            lock (lockObject)
            {
                if (failureInfoPerMessage.TryGetValue(messageId, out var node))
                {
                    // We have seen this message before, just update the counter and store exception.
                    node.FailureInfo = new ProcessingFailureInfo(node.FailureInfo.NumberOfProcessingAttempts + 1, ExceptionDispatchInfo.Capture(exception), context);

                    // Maintain invariant: leastRecentlyUsedMessages.First contains the LRU item.
                    leastRecentlyUsedMessages.Remove(node.LeastRecentlyUsedEntry);
                    leastRecentlyUsedMessages.AddLast(node.LeastRecentlyUsedEntry);
                }
                else
                {
                    if (failureInfoPerMessage.Count == maxElements)
                    {
                        // We have reached the maximum allowed capacity. Remove the LRU item.
                        var leastRecentlyUsedEntry = leastRecentlyUsedMessages.First;
                        failureInfoPerMessage.Remove(leastRecentlyUsedEntry.Value);
                        leastRecentlyUsedMessages.RemoveFirst();
                    }

                    var newNode = new FailureInfoNode(
                        messageId,
                        new ProcessingFailureInfo(1, ExceptionDispatchInfo.Capture(exception), context));

                    failureInfoPerMessage[messageId] = newNode;

                    // Maintain invariant: leastRecentlyUsedMessages.First contains the LRU item.
                    leastRecentlyUsedMessages.AddLast(newNode.LeastRecentlyUsedEntry);
                }
            }
        }

        public bool TryGetFailureInfoForMessage(string messageId, out ProcessingFailureInfo processingFailureInfo)
        {
            lock (lockObject)
            {
                if (failureInfoPerMessage.TryGetValue(messageId, out var node))
                {
                    processingFailureInfo = node.FailureInfo;
                    return true;
                }
                processingFailureInfo = null;
                return false;
            }
        }

        public void ClearFailureInfoForMessage(string messageId)
        {
            lock (lockObject)
            {
                failureInfoPerMessage.Remove(messageId);
                leastRecentlyUsedMessages.Remove(messageId);
            }
        }

        readonly Dictionary<string, FailureInfoNode> failureInfoPerMessage = [];
        readonly LinkedList<string> leastRecentlyUsedMessages = [];
        readonly Lock lockObject = new();

        class FailureInfoNode(string messageId, ProcessingFailureInfo failureInfo)
        {
            public ProcessingFailureInfo FailureInfo { get; set; } = failureInfo;
            public LinkedListNode<string> LeastRecentlyUsedEntry { get; } = new(messageId);
        }

        public class ProcessingFailureInfo(int numberOfProcessingAttempts, ExceptionDispatchInfo exceptionDispatchInfo, ContextBag context)
        {
            public int NumberOfProcessingAttempts { get; } = numberOfProcessingAttempts;
            public Exception Exception => ExceptionDispatchInfo.SourceException;
            ExceptionDispatchInfo ExceptionDispatchInfo { get; } = exceptionDispatchInfo;
            public ContextBag Context { get; } = context;
        }
    }
}