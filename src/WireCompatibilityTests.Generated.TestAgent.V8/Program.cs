namespace TestAgent.V8
{
    using System.Threading.Tasks;
    using Framework;

    class Program
    {
        static Task Main(string[] args)
        {
            return TestAgentFacade.Run(args);
        }
    }
}
