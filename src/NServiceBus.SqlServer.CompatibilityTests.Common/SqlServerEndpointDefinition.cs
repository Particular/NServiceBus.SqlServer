namespace CompatibilityTests.Common
{
    using System;

    [Serializable]
    public class EndpointDefinition
    {
        public string Name { get; set; }
        public string MachineName { get; set; }
        public EndpointDefinition()
        {
        }

        public EndpointDefinition(string name)
        {
            Name = name;
        }
    }
}
