namespace TestLogicApi
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;

    /// <summary>
    /// Represents a behavior of an endpoint within a test suite
    /// </summary>
    public interface ITestBehavior
    {
#pragma warning disable PS0018
        Task Execute(IEndpointInstance endpointInstance);
#pragma warning restore PS0018
        EndpointConfiguration Configure(Dictionary<string, string> args);
    }
}