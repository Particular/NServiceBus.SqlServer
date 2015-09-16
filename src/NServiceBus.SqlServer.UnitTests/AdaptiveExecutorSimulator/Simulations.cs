namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    public class Simulations
    {
        bool RunRealSenderAndReceiver = false;

        [Test]
        public void TestSlowMessages()
        {
            var workload = new Load()
                .AddStage(1000, 50, () => 60*60*1000);

            var simulator = CreateSimulator();
            var results = simulator.Simulate(workload, 10);
            DumpToConsole(results);
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
                        return 12*60*100;
                    }
                    if (randomValue < 30)
                    {
                        return 1*60*100;
                    }
                    return 10;
                });

            var simulator = CreateSimulator();
            var results = simulator.Simulate(workload, 10);
            DumpToConsole(results);
        }
        
        static void DumpToConsole(IEnumerable<string> results)
        {
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }

        private ISimulator CreateSimulator()
        {
            if (RunRealSenderAndReceiver)
            {
                return new RealSimulator("SQLPerfTest", @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
            }
            return new Simulator();
        }
    }
}