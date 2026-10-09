using NServiceBus.Transport.PostgreSql;
using NUnit.Framework;

static class SubscriptionTableNameCreator
{
    // Acceptance fixtures run in parallel and each test drops its subscription table on cleanup, so every test gets its own.
    public static SubscriptionTableName CreateDefault() =>
        new($"SubscriptionRouting_{TestContext.CurrentContext.Test.ID.Replace('-', '_')}", "public");
}
