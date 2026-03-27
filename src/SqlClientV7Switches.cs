using System;
using System.Reflection;
using System.Runtime.CompilerServices;

static class SqlClientV7Switches
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var switchType = typeof(Microsoft.Data.SqlClient.SqlConnection).Assembly
            .GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches", throwOnError: true);

        SetSwitch(switchType, "UseCompatibilityProcessSniString");
        SetSwitch(switchType, "UseCompatibilityAsyncBehaviourString");
    }

    static void SetSwitch(Type switchType, string fieldName)
    {
        var field = switchType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Could not find field '{fieldName}' on {switchType.FullName}. " +
                "The switch may have been removed or renamed in this version of Microsoft.Data.SqlClient, " +
                "which likely means the improved behavior is now the default.");

        var switchName = (string)field.GetValue(null);
        AppContext.SetSwitch(switchName, false);
    }
}
