using System;
using NServiceBus;
using NServiceBus.Transport.SqlServer;

// Lets CI run the whole suite against either queue table layout. The transport tests compile this file too.
static class QueueTableLayoutTestSwitch
{
    public static SqlServerTransport ApplyQueueTableLayoutTestSwitch(this SqlServerTransport transport)
    {
        transport.CreateBucketedQueueTables = CreateBucketedQueueTables;
        return transport;
    }

    public static string CreateQueueScript(SqlServerConstants sqlConstants) =>
        CreateBucketedQueueTables ? sqlConstants.CreateBucketedQueueText : sqlConstants.CreateQueueText;

    static bool CreateBucketedQueueTables
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("SqlServerTransport_CreateBucketedQueueTables");

            // CI leaves the variable empty in the heap jobs. bool.Parse throws on a typo instead of silently testing heaps.
            return !string.IsNullOrEmpty(value) && bool.Parse(value);
        }
    }
}
