namespace CompatibilityTests.Common
{
    using System;

    [Serializable]
    public abstract class EndpointDefinition
    {
        public abstract string TransportName { get; }
        public string Name { get; set; }
    }
}
