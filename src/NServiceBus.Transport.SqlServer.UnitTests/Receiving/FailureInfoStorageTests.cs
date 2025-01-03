﻿namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving
{
    using System;
    using System.Collections.Generic;
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

        static FailureInfoStorage GetFailureInfoStorage()
        {
            return new FailureInfoStorage(10);
        }
    }
}