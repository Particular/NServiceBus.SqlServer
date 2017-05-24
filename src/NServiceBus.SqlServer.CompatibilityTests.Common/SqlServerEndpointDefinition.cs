namespace CompatibilityTests.Common
{
    using System;

    [Serializable]
    public class EndpointDefinition
    {
        public string Name { get; private set; }
        public string MachineName { get; set; }

        public EndpointDefinition(string name)
        {
            Name = name;
        }
    }
}
