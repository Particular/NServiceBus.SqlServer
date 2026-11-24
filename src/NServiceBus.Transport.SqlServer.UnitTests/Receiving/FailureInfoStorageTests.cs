namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class FailureInfoStorageTests
{
    [Test]
    public void When_recording_failure_initially_should_store_one_failed_attempt_and_exception()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var exception = new Exception();
        var extensions = new ContextBag();

        var storage = GetFailureInfoStorage();

        storage.RecordFailureInfoForMessage(messageId, exception, extensions);

        storage.TryGetFailureInfoForMessage(messageId, out var failureInfo);

        Assert.That(failureInfo, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(failureInfo.NumberOfProcessingAttempts, Is.EqualTo(1));
            Assert.That(failureInfo.Exception, Is.SameAs(exception));
            Assert.That(failureInfo.Context, Is.SameAs(extensions));
        });
    }

    [Test]
    public void When_recording_failure_many_times_should_store_number_of_attempts_and_last_exception()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var secondException = new Exception();

        var storage = GetFailureInfoStorage();

        storage.RecordFailureInfoForMessage(messageId, new Exception(), default);
        storage.RecordFailureInfoForMessage(messageId, secondException, default);

        storage.TryGetFailureInfoForMessage(messageId, out var failureInfo);

        Assert.That(failureInfo, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(failureInfo.NumberOfProcessingAttempts, Is.EqualTo(2));
            Assert.That(failureInfo.Exception, Is.SameAs(secondException));
        });
    }

    [Test]
    public void When_clearing_failure_should_return_null_on_subsequent_retrieval()
    {
        var messageId = Guid.NewGuid().ToString("D");

        var storage = GetFailureInfoStorage();

        storage.RecordFailureInfoForMessage(messageId, new Exception(), default);


        storage.TryGetFailureInfoForMessage(messageId, out var failureInfo);
        Assert.That(failureInfo, Is.Not.Null);

        storage.ClearFailureInfoForMessage(messageId);

        storage.TryGetFailureInfoForMessage(messageId, out failureInfo);
        Assert.That(failureInfo, Is.Null);
    }

    [Test]
    public void When_clearing_failure_for_unknown_message_should_not_affect_other_entries()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var storage = GetFailureInfoStorage();

        storage.RecordFailureInfoForMessage(messageId, new Exception(), default);

        Assert.DoesNotThrow(() => storage.ClearFailureInfoForMessage(Guid.NewGuid().ToString("D")));
        Assert.That(storage.TryGetFailureInfoForMessage(messageId, out _), Is.True);
    }

    [Test]
    public void When_recording_more_than_max_number_of_failures_should_remove_least_recently_used_entry()
    {
        const int MaxElements = 50;
        var storage = new FailureInfoStorage(maxElements: MaxElements);

        var lruMessageId = Guid.NewGuid().ToString("D");

        storage.RecordFailureInfoForMessage(lruMessageId, new Exception(), default);

        for (var i = 0; i < MaxElements; ++i)
        {
            var messageId = Guid.NewGuid().ToString("D");
            var exception = new Exception();

            storage.RecordFailureInfoForMessage(messageId, exception, default);
        }

        storage.TryGetFailureInfoForMessage(lruMessageId, out var failureInfo);

        Assert.That(failureInfo, Is.Null);
    }

    [Test]
    public void When_recording_a_failure_for_a_message_it_should_not_be_treated_as_least_recently_used()
    {
        const int MaxElements = 50;
        var storage = new FailureInfoStorage(MaxElements);

        var lruMessageId = Guid.NewGuid().ToString("D");

        storage.RecordFailureInfoForMessage(lruMessageId, new Exception(), default);

        var messageIds = new List<string>(MaxElements);
        for (var i = 0; i < MaxElements; ++i)
        {
            messageIds.Add(Guid.NewGuid().ToString("D"));
        }

        for (var i = 0; i < MaxElements - 1; ++i)
        {
            storage.RecordFailureInfoForMessage(messageIds[i], new Exception(), default);
        }

        storage.RecordFailureInfoForMessage(lruMessageId, new Exception(), default);

        storage.RecordFailureInfoForMessage(messageIds[MaxElements - 1], new Exception(), default);

        storage.TryGetFailureInfoForMessage(lruMessageId, out var failureInfo);

        Assert.That(failureInfo, Is.Not.Null);
    }

    [Test]
    public void When_recording_many_more_failures_than_max_should_keep_most_recent_and_drop_oldest()
    {
        const int MaxElements = 50;
        var storage = new FailureInfoStorage(MaxElements);

        var messageIds = new List<string>();
        for (var i = 0; i < MaxElements * 10; ++i)
        {
            var messageId = Guid.NewGuid().ToString("D");
            messageIds.Add(messageId);
            storage.RecordFailureInfoForMessage(messageId, new Exception(), default);
        }

        Assert.Multiple(() =>
        {
            Assert.That(storage.TryGetFailureInfoForMessage(messageIds[0], out _), Is.False);
            Assert.That(storage.TryGetFailureInfoForMessage(messageIds[^1], out _), Is.True);
        });
    }

    [Test]
    public void When_recording_failures_for_different_messages_concurrently_should_count_attempts_per_message()
    {
        const int Messages = 1000;
        const int Attempts = 3;
        var storage = new FailureInfoStorage(Messages * 2);
        var messageIds = Enumerable.Range(0, Messages).Select(_ => Guid.NewGuid().ToString("D")).ToArray();

        Parallel.ForEach(messageIds, messageId =>
        {
            for (var attempt = 0; attempt < Attempts; ++attempt)
            {
                storage.RecordFailureInfoForMessage(messageId, new Exception(), default);
            }
        });

        foreach (var messageId in messageIds)
        {
            Assert.That(storage.TryGetFailureInfoForMessage(messageId, out var failureInfo), Is.True);
            Assert.That(failureInfo.NumberOfProcessingAttempts, Is.EqualTo(Attempts));
        }
    }

    [Test]
    public void When_expiry_is_configured_should_remove_failure_info_after_the_expiry()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var storage = new FailureInfoStorage(10, TimeSpan.FromMilliseconds(50));

        storage.RecordFailureInfoForMessage(messageId, new Exception(), default);

        Assert.That(() => storage.TryGetFailureInfoForMessage(messageId, out _), Is.False.After(5000, 10));
    }

    [Test]
    public void When_expiry_is_configured_should_keep_failure_info_before_the_expiry()
    {
        var messageId = Guid.NewGuid().ToString("D");
        var storage = new FailureInfoStorage(10, TimeSpan.FromHours(1));

        storage.RecordFailureInfoForMessage(messageId, new Exception(), default);
        storage.RecordFailureInfoForMessage(messageId, new Exception(), default);

        Assert.That(storage.TryGetFailureInfoForMessage(messageId, out var failureInfo), Is.True);
        Assert.That(failureInfo.NumberOfProcessingAttempts, Is.EqualTo(2));
    }

    [Test]
    public void When_expiry_is_configured_should_still_limit_the_number_of_entries()
    {
        const int MaxElements = 50;
        var storage = new FailureInfoStorage(MaxElements, TimeSpan.FromHours(1));
        var messageIds = Enumerable.Range(0, MaxElements * 10).Select(_ => Guid.NewGuid().ToString("D")).ToArray();

        foreach (var messageId in messageIds)
        {
            storage.RecordFailureInfoForMessage(messageId, new Exception(), default);
        }

        var stored = messageIds.Count(messageId => storage.TryGetFailureInfoForMessage(messageId, out _));
        Assert.That(stored, Is.LessThanOrEqualTo(MaxElements));
    }

    [Test]
    public void When_trimming_is_started_should_release_expired_failure_info_without_cache_access()
    {
        var storage = new FailureInfoStorage(10, TimeSpan.FromMilliseconds(50));
        var recordedException = RecordFailure(storage);

        storage.StartTrimmingExpiredEntries();

        try
        {
            Assert.That(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return recordedException.IsAlive;
            }, Is.False.After(10000, 200));
        }
        finally
        {
            storage.StopTrimmingExpiredEntries();
        }
    }

    [Test]
    public void Starting_and_stopping_trimming_should_be_safe_without_expiry_and_when_repeated()
    {
        var storage = new FailureInfoStorage(10);
        var expiringStorage = new FailureInfoStorage(10, TimeSpan.FromHours(1));

        Assert.DoesNotThrow(() =>
        {
            storage.StartTrimmingExpiredEntries();
            storage.StopTrimmingExpiredEntries();
            expiringStorage.StopTrimmingExpiredEntries();
            expiringStorage.StartTrimmingExpiredEntries();
            expiringStorage.StartTrimmingExpiredEntries();
            expiringStorage.StopTrimmingExpiredEntries();
            expiringStorage.StopTrimmingExpiredEntries();
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference RecordFailure(FailureInfoStorage storage)
    {
        var exception = new Exception();
        storage.RecordFailureInfoForMessage(Guid.NewGuid().ToString("D"), exception, default);
        return new WeakReference(exception);
    }

    static FailureInfoStorage GetFailureInfoStorage() => new(10);
}