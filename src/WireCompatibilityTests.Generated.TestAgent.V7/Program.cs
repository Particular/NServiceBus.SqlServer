using System.Threading.Tasks;
using TestAgent.Framework;

public class Program
{
    static Task Main(string[] args)
    {
        return TestAgentFacade.Run(args);
    }
}
