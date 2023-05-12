namespace TestRunner
{
    using System.Collections.Generic;
    using NuGet.Versioning;

    public class AgentInfo
    {
        public SemanticVersion Version { get; set; }
        public string Behavior { get; set; }
        public Dictionary<string, string> BehaviorParameters { get; set; }

        public static AgentInfo Create(
            string behavior,
            SemanticVersion version,
            Dictionary<string, string> behaviorParameters)
        {
            if (behavior.StartsWith('I'))
            {
                behavior = behavior.Substring(1);
            }

            return new AgentInfo
            {
                Behavior = behavior,
                Version = version,
                BehaviorParameters = behaviorParameters
            };
        }
    }
}
