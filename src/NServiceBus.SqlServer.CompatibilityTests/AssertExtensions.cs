namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Threading;
    using NUnit.Framework;

    public static class AssertEx
    {
        /// <summary>
        ///     Executes a task returns when done.
        ///     <exception cref="AssertionException">Throws when task wasn't done within 20 seconds.</exception>
        /// </summary>
        /// <param name="predicate">Task to execute. A Func for backwards compatibility reasons.</param>
        /// <param name="timeout">Override default timeout of 90 seconds.</param>
        public static void WaitUntilIsTrue(Func<bool> predicate, TimeSpan? timeout = null)
        {
            if (timeout.HasValue == false)
            {
                timeout = TimeSpan.FromSeconds(90);
            }

            var waitUntil = DateTime.Now + timeout.Value;

            while (DateTime.Now < waitUntil)
            {
                if (predicate())
                {
                    return;
                }
            }
            throw new AssertionException($"Condition has not been met for {timeout.Value.TotalSeconds} seconds.");
        }

        /// <summary>
        ///     Executes a task returns when done oe timed out.
        /// </summary>
        /// <param name="predicate">Task to execute. A Func for backwards compatibility reasons.</param>
        /// <param name="timeout">Override default timeout of 90 seconds.</param>
        public static bool TryWaitUntilIsTrue(Func<bool> predicate, TimeSpan? timeout = null)
        {
            if (timeout.HasValue == false)
            {
                timeout = TimeSpan.FromSeconds(90);
            }
            return SpinWait.SpinUntil(predicate, timeout.Value);
        }
    }
}