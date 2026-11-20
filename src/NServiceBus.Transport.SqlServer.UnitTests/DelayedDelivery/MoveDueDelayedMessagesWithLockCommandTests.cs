namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using Microsoft.Data.SqlClient;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [Test]
    public void Populates_command_with_the_batch_size_and_lock_resource_parameters()
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "[delayed]", "[input]");
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "[delayed]", "[input]")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            Assert.That(command.Parameters["MoverLockResource"].Value, Is.EqualTo("[delayed]_mover"));
            Assert.That(command.Parameters.Count, Is.EqualTo(2));
        }
    }

    [Test]
    public void Owns_the_transaction() =>
        Assert.That(new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), "[delayed]", "[input]").OwnsTransaction, Is.True);

    [Test]
    public void Keeps_a_table_name_with_an_apostrophe_out_of_the_command_text()
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, "[o'brien.delayed]", "[input]");
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Parameters["MoverLockResource"].Value, Is.EqualTo("[o'brien.delayed]_mover"));
            Assert.That(command.CommandText, Does.Not.Contain("'[o'brien.delayed]"));
        }
    }
}
