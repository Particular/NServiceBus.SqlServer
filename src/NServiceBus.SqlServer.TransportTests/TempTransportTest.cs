namespace NServiceBus.TransportTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Logging;
    using NUnit.Framework;
    using Transport;

    public class When_on_error_throws_DEBUG : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_invoke_critical_error_and_retry(TransportTransactionMode transactionMode)
        {
            var criticalErrorCalled = false;
            string criticalErrorMessage = null;
            string nativeMessageId = null;
            Exception criticalErrorException = null;
            var exceptionFromOnError = new Exception("Exception from onError");

            var completed = new TaskCompletionSource<CompleteContext>();
            OnTestTimeout(() => completed.SetCanceled());

            var retrying = false;
            var retried = false;

            await StartPump(
                (context, _) =>
                {
                    if (retrying)
                    {
                        retried = true;
                        return Task.CompletedTask;
                    }

                    nativeMessageId = context.NativeMessageId;

                    throw new Exception("Exception from onMessage");
                },
                (context, _) =>
                {
                    retrying = true;
                    throw exceptionFromOnError;
                },
                (_, __) => retried ? completed.SetCompleted() : Task.CompletedTask,
                transactionMode,
                (message, exception, _) =>
                {
                    criticalErrorCalled = true;
                    criticalErrorMessage = message;
                    criticalErrorException = exception;
                });
            ;

            LogFactory.LogItems.Clear();

            await SendMessage(InputQueueName);

            _ = await completed.Task;

            Assert.True(criticalErrorCalled, "Should invoke critical error");
            Assert.AreEqual($"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`", criticalErrorMessage);
            Assert.AreEqual(exceptionFromOnError, criticalErrorException);

            var logItemsAboveInfo = LogFactory.LogItems.Where(item => item.Level > LogLevel.Info).Select(log => $"{log.Level}: {log.Message}").ToArray();
            Assert.AreEqual(0, logItemsAboveInfo.Length, "Transport should not log anything above LogLevel.Info:" + string.Join(Environment.NewLine, logItemsAboveInfo));
        }
    }
}

namespace NServiceBus.TransportTests
{
    using System.Threading.Tasks;
    using System.Transactions;
    using NUnit.Framework;
    using Transport;

    public class When_transaction_level_TransactionScope_DEBUG : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_have_active_transaction(TransportTransactionMode transactionMode)
        {
            Transaction currentTransaction = null;
            Transaction contextTransaction = null;

            var completed = new TaskCompletionSource<bool>();
            OnTestTimeout(() => completed.SetCanceled());

            await StartPump(
                (context, _) =>
                {
                    currentTransaction = Transaction.Current;
                    contextTransaction = context.TransportTransaction.Get<Transaction>();
                    return Task.CompletedTask;
                },
                (_, __) => Task.FromResult(ErrorHandleResult.Handled),
                (_, __) =>
                {
                    completed.SetResult(true);
                    return Task.CompletedTask;
                },
                transactionMode);

            await SendMessage(InputQueueName);

            _ = await completed.Task;

            Assert.NotNull(currentTransaction);
            Assert.AreSame(currentTransaction, contextTransaction);
        }
    }
}
