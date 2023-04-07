
namespace TestRunner
{
    using System.Collections.Generic;

    public class AgentInfo
    {
        public string Project { get; set; }
        public string Behavior { get; set; }
        public Dictionary<string, string> BehaviorParameters { get; set; }

        public static AgentInfo Create(string version, string behavior)
        {
            return new AgentInfo
            {
                Behavior = $"WireCompatibilityTests.TestBehaviors.{version}.{behavior}, WireCompatibilityTests.TestBehaviors.{version}",
                Project = $"WireCompatibilityTests.Generated.TestAgent.{version}"
            };
        }
    }

}
