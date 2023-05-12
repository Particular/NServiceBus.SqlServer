
namespace TestRunner
{
    using System;
    using System.Collections.Generic;

    public class AgentInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int CoreMajor { get; set; }
        public string Behavior { get; set; }
        public Dictionary<string, string> BehaviorParameters { get; set; }

        public static AgentInfo Create(string behavior, string version, int coreMajor, Dictionary<string, string> behaviorParameters)
        {
            if (behavior.StartsWith('I'))
            {
                behavior = behavior.Substring(1);
            }

            var parts = version.Split(".", StringSplitOptions.RemoveEmptyEntries);
            return new AgentInfo
            {
                Behavior = behavior,
                Major = int.Parse(parts[0]),
                Minor = int.Parse(parts[1]),
                CoreMajor = coreMajor,
                BehaviorParameters = behaviorParameters
            };
        }
    }
}
