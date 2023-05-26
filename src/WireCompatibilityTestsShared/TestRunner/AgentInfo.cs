namespace TestRunner
{
    using NuGet.Versioning;

    public class AgentInfo
    {
        public SemanticVersion Version { get; set; }
        public string Behavior { get; set; }
        public PluginOptions BehaviorParameters { get; set; }

        public static AgentInfo Create(
            string behavior,
            SemanticVersion version,
            PluginOptions opts
            )
        {
            return new AgentInfo
            {
                Behavior = behavior,
                Version = version,
                BehaviorParameters = opts
            };
        }
    }
}
