namespace NServiceBus.SqlServer.UnitTests
{
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class BoundedExponentialBackOffTests
    {
        [Test]
        public void It_does_not_wait_if_condition_evaluates_to_false()
        {
            var waiter = new Waiter();
            var backOff = new BoundedExponentialBackOff(1000);

            backOff.ConditionalWait(() => false, waiter.Wait);

            Assert.AreEqual(0, waiter.LastWaitTime);
        }
        
        [Test]
        public void It_waits_initial_value_if_condition_evaluates_to_true()
        {
            var waiter = new Waiter();
            var backOff = new BoundedExponentialBackOff(1000);

            backOff.ConditionalWait(() => true, waiter.Wait);

            Assert.AreEqual(50, waiter.LastWaitTime);
        }
        
        [Test]
        public void It_waits_more_if_condition_evaluates_to_true()
        {
            var waiter = new Waiter();
            var backOff = new BoundedExponentialBackOff(1000);

            backOff.ConditionalWait(() => true, _ => { });
            backOff.ConditionalWait(() => true, waiter.Wait);

            Assert.AreEqual(100, waiter.LastWaitTime);
        }
        
        [Test]
        public void It_waits_no_more_than_maximum_value()
        {
            var waiter = new Waiter();
            var backOff = new BoundedExponentialBackOff(100);

            backOff.ConditionalWait(() => true, _ => { });
            backOff.ConditionalWait(() => true, _ => { });
            backOff.ConditionalWait(() => true, waiter.Wait);

            Assert.AreEqual(100, waiter.LastWaitTime);
        }

        private class Waiter
        {
            public int LastWaitTime { get; private set; }

            public void Wait(int delay)
            {
                LastWaitTime = delay;
            }
        }
    }
}