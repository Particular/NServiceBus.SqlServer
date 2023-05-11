namespace WireCompatibilityTests;

using System.Collections;
using System.Collections.Generic;

#pragma warning disable CA1710
public class TestCaseGenerator : IEnumerable
#pragma warning restore CA1710
{
    readonly Dictionary<string, int> includedVersions = new()
    {
        //{"4.0", 7},
        //{"4.1", 7},
        //{"4.2", 7},
        //{"4.3", 7},
        //{"5.0", 7},
        //{"6.0", 7},
        //{"6.1", 7},
        //{"6.2", 7},
        {"6.3", 7},
        {"7.0", 8},
    };

    public IEnumerator GetEnumerator()
    {
        foreach (var v1 in includedVersions)
        {
            foreach (var v2 in includedVersions)
            {
                if (v1.Key != v2.Key)
                {
                    yield return new object[] { v1.Key, v1.Value, v2.Key, v2.Value };
                }
            }
        }
    }
}