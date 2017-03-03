namespace CompatibilityTests.Common
{
    using System;

    public interface IEndpointConfiguration
    {
        void UseConnectionString(string connectionString);
        void MapMessageToEndpoint(Type messageType, string destination);
        void Start();
    }

    public interface IEndpointConfigurationV1 : IEndpointConfiguration
    {
        void UseConnectionStringForEndpoint(string endpoint, string connectionString);
    }

    public interface IEndpointConfigurationV2 : IEndpointConfiguration
    {
        void DefaultSchema(string schema);
        void UseSchemaForTransportAddress(string transportAddress, string schema);
    }

    public interface IEndpointConfigurationV3 : IEndpointConfiguration
    {
        void EnableCallbacks();
        void DefaultSchema(string schema);
        void UseSchemaForQueue(string queue, string schema);
        void RouteToEndpoint(Type messageType, string endpoint);
    }
}