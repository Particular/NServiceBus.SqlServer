namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class MoveDueDelayedMessagesCommandTests
{
    [Test]
    public void Leaves_the_transaction_to_the_caller() =>
        Assert.That(new MoveDueDelayedMessagesCommand(new SqlServerConstants(), "[delayed]", "[input]").OwnsTransaction, Is.False);
}
