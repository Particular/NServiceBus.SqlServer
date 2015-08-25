namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class Simulations
    {
        [Test]
        public void TestSlowMessages()
        {
            var workload = new Load()
                .AddStage(1000, 50, () => 60*60*1000);

            var simulator = new Simulator(10, rampUp => new ReceiveRampUpController(rampUp, new TransportNotifications(), "SomeQueue"), workload);
            simulator.Simulate();
            simulator.DumpResultsToConsole();
        }
        
        [Test]
        public void TestVariableTimes()
        {
            var random = new Random(133);
            var workload = new Load()
                .AddStage(1000, 50, () =>
                {
                    var randomValue = random.Next(100);
                    if (randomValue < 10)
                    {
                        return 60*60*1000;
                    }
                    if (randomValue < 30)
                    {
                        return 5*60*1000;
                    }
                    return 500;
                });

            var simulator = new Simulator(10, rampUp => new ReceiveRampUpController(rampUp, new TransportNotifications(), "SomeQueue"), workload);
            simulator.Simulate();
            simulator.DumpResultsToConsole();
        }
    }
}