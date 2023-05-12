namespace WireCompatibilityTests;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public class GeneratedVersionsSet : IEnumerable
{
    public readonly string PackageId = "NServiceBus.SqlServer";
    public readonly SemanticVersion MinVersion = new(6, 0, 0);

    public IEnumerator GetEnumerator()
    {
        using var cache = new SourceCacheContext { NoCache = true };

        //string source = "https://www.myget.org/F/particular/api/v3/index.json";
        string source = "https://api.nuget.org/v3/index.json";
        var nuget = Repository.Factory.GetCoreV3(source);
        var resources = nuget.GetResource<FindPackageByIdResource>();

        var versions = resources.GetAllVersionsAsync(PackageId, cache, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();

        // Get all minors
        versions = versions.Where(v => !v.IsPrerelease && v >= MinVersion).OrderBy(v => v);

        NuGetVersion last = null;

        var latestMinors = new HashSet<NuGetVersion>();

        foreach (var v in versions)
        {
            if (last == null)
            {
                last = v;
                continue;
            }

            if (last.Major != v.Major)
            {
                latestMinors.Add(last);
            }
            else if (last.Minor != v.Minor)
            {
                latestMinors.Add(last);
            }

            last = v;
        }

        latestMinors.Add(last);

        foreach (var a in latestMinors)
        {
            foreach (var b in latestMinors)
            {
                yield return new object[] { a, b };
            }
        }
    }
}