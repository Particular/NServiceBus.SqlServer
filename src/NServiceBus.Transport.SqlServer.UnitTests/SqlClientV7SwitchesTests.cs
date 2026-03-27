namespace NServiceBus.Transport.SqlServer.UnitTests;

using System;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// These tests verify that the SqlClient compatibility switches still exist and are still opt-in.
/// When these tests fail, the improved async/SNI behavior has likely become the default in
/// Microsoft.Data.SqlClient and SqlClientV7Switches.cs can be removed.
/// See https://github.com/dotnet/SqlClient/issues/593
/// </summary>
[TestFixture]
public class SqlClientV7SwitchesTests
{
    static readonly Type SwitchType = typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly
        .GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");

    [Test]
    public void LocalAppContextSwitches_type_should_exist()
    {
        Assert.That(SwitchType, Is.Not.Null,
            "LocalAppContextSwitches type no longer exists in Microsoft.Data.SqlClient. " +
            "Review whether SqlClientV7Switches.cs can be removed.");
    }

    [TestCase("UseCompatibilityProcessSniString")]
    [TestCase("UseCompatibilityAsyncBehaviourString")]
    public void Compatibility_switch_field_should_exist(string fieldName)
    {
        var field = SwitchType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null,
            $"Field '{fieldName}' no longer exists on LocalAppContextSwitches. " +
            "The improved behavior is likely now the default and SqlClientV7Switches.cs can be removed.");
    }
}
