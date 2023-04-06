
namespace TestRunner
{
    using System.Collections.Generic;

    public class AgentInfo
    {
        public string Project { get; set; }
        public string Behavior { get; set; }
        public Dictionary<string, string> BehaviorParameters { get; set; }

        public static AgentInfo Create(string version)
        {
            return new AgentInfo
            {
                Behavior = $"WireCompatibilityTests.TestBehaviors.{version}.Sender, WireCompatibilityTests.TestBehaviors.{version}",
                Project = $"WireCompatibilityTests.Generated.TestAgent.{version}"
            };
        }
    }

}
