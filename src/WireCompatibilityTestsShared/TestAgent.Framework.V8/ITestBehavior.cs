namespace TestLogicApi
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;

    /// <summary>
    /// Represents a behavior of an endpoint within a test suite
    /// </summary>
    public interface ITestBehavior
    {
        Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default);
        EndpointConfiguration Configure(Dictionary<string, string> args);
    }
}