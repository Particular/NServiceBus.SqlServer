namespace NServiceBus.SqlServer.UnitTests
{
    using System.Collections.Generic;

    interface ISimulator
    {
        IEnumerable<string> Simulate(Load workLoad, int maximumConcurrency);
    }
}