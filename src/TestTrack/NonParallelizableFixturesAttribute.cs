using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

// For fixtures in the shared acceptance sources, which cannot be annotated directly.
[AttributeUsage(AttributeTargets.Assembly)]
sealed class NonParallelizableFixturesAttribute(params Type[] fixtures) : Attribute, IApplyToTest
{
    public void ApplyToTest(Test test) => Apply(test);

    void Apply(ITest test)
    {
        if (test is Test fixture && test.IsSuite && test.TypeInfo is not null && Array.IndexOf(fixtures, test.TypeInfo.Type) >= 0)
        {
            new NonParallelizableAttribute().ApplyToTest(fixture);
        }

        foreach (var child in test.Tests)
        {
            Apply(child);
        }
    }
}
