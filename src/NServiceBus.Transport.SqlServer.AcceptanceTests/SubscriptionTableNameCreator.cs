using NServiceBus.Transport.SqlServer;

static class SubscriptionTableNameCreator
{
    public static SubscriptionTableName CreateDefault() => new("SubscriptionRouting", "dbo");
}