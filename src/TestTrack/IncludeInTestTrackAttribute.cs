using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Assembly)]
sealed class IncludeInTestTrackAttribute(string track) : Attribute, IApplyToContext
{
    public void ApplyToContext(TestExecutionContext context)
    {
        var selectedTrack = Environment.GetEnvironmentVariable("SqlServerTransport_TESTS_TRACK");

        // Unset runs everything, which is what a local run wants.
        if (!string.IsNullOrEmpty(selectedTrack) && !string.Equals(selectedTrack, track, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Ignoring because SqlServerTransport_TESTS_TRACK is '{selectedTrack}' and this assembly belongs to '{track}'.");
        }
    }
}
