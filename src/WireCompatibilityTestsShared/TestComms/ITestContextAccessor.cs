namespace TestComms
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface used to coordinate between test agents that take part in the test
    /// </summary>
    public interface ITestContextAccessor
    {
        void SetFlag(string name, bool value);
        bool GetFlag(string name);
        void Set(string name, int value);
        void Increment(string name, int value);
        int Get(string name);
        Task<bool> WaitUntilTrue(string flagName, CancellationToken cancellationToken = default);
        void Success();
        void Failure();
        Dictionary<string, object> ToDictionary();
    }
}
