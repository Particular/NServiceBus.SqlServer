using NServiceBus;
using NServiceBus.Transport.Sql.Shared.DelayedDelivery;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    public void Approve()
    {
        var publicApi = typeof(SqlServerTransport).Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            ExcludeAttributes = new[]
            {
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                "System.Reflection.AssemblyMetadataAttribute"
            }
        });
        Approver.Verify(publicApi);
    }

    [Test]
    public void ApproveShared()
    {
        var publicApi = typeof(BackOffStrategy).Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            ExcludeAttributes = new[]
            {
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                "System.Reflection.AssemblyMetadataAttribute"
            }
        });
        Approver.Verify(publicApi);
    }
}