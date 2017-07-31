namespace CompatibilityTests.Common
{
    using System;

    [Serializable]
    public class EndpointDefinition
    {
        public string Name { get; }
        public string MachineName { get; set; }

        public EndpointDefinition(string name)
        {
            Name = name;
        }
    }
}
