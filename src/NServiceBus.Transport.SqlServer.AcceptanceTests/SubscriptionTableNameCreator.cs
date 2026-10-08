using NServiceBus.Transport.SqlServer;
using NUnit.Framework;

static class SubscriptionTableNameCreator
{
    // Acceptance fixtures run in parallel and each test drops its subscription table on cleanup, so every test gets its own.
    public static SubscriptionTableName CreateDefault(string catalog = null) =>
        new($"SubscriptionRouting_{TestContext.CurrentContext.Test.ID.Replace('-', '_')}", "dbo", catalog);
}
