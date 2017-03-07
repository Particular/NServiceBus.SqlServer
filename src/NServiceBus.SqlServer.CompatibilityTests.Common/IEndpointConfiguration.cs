namespace CompatibilityTests.Common
{
    using System;
    using System.Collections.Generic;

    public interface IEndpointConfiguration
    {
        void UseConnectionString(string connectionString);
        void MapMessageToEndpoint(Type messageType, string destination);
        void Start();
    }

    public interface IEndpointConfigurationV1 : IEndpointConfiguration
    {
        void ConfigureNamedConnectionStringForAddress(string endpoint, string connectionString);
    }

    public interface IEndpointConfigurationV2 : IEndpointConfiguration
    {
        void EnableCallbacks();
        void DefaultSchema(string schema);
        void UseSchemaForTransportAddress(string transportAddress, string schema);
        void UseConnectionStringForAddress(string transportAddress, string connectionString);
    }

    public interface IEndpointConfigurationV3 : IEndpointConfiguration
    {
        void EnableCallbacks(string instanceId);
        void DefaultSchema(string schema);
        void UseSchemaForQueue(string queue, string schema);
        void UseSchemaForEndpoint(string endpoint, string schema);
        void RouteToEndpoint(Type messageType, string endpoint);
        void UseLagacyMultiInstanceMode(Dictionary<string, string> connectionStringMap);
    }
}