namespace NServiceBus.Transport.SqlServer.UnitTests.DelayedDelivery;

using Microsoft.Data.SqlClient;
using NUnit.Framework;

public class MoveDueDelayedMessagesWithLockCommandTests
{
    [Test]
    public void Populates_command_with_the_batch_size_lock_resource_and_lock_catalog_parameters()
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, delayedQueue, "[input]");
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Is.EqualTo(string.Format(sqlConstants.MoveDueDelayedMessageWithLockText, "[delayedcatalog].[dbo].[delayed]", "[input]", "[delayedcatalog]")));
            Assert.That(command.Parameters["BatchSize"].Value, Is.EqualTo(42));
            Assert.That(command.Parameters["MoverLockResource"].Value, Is.EqualTo("[DELAYEDCATALOG].[DBO].[DELAYED]_MOVER"));
            Assert.That(command.Parameters["MoverLockCatalog"].Value, Is.EqualTo("delayedcatalog"));
            Assert.That(command.Parameters.Count, Is.EqualTo(3));
        }
    }

    [Test]
    public void Owns_the_transaction() =>
        Assert.That(new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), delayedQueue, "[input]").OwnsTransaction, Is.True);

    [Test]
    public void Uses_the_same_lock_resource_for_table_names_that_differ_only_in_case()
    {
        var lower = new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), new CanonicalQueueAddress("delayed", "dbo", "nservicebus"), "[input]");
        var upper = new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), new CanonicalQueueAddress("DELAYED", "DBO", "NServiceBus"), "[input]");

        Assert.That(upper.LockResource, Is.EqualTo(lower.LockResource));
    }

    [Test]
    public void Runs_the_lock_in_the_catalog_of_the_delayed_table()
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, new CanonicalQueueAddress("delayed", "dbo", "other]catalog"), "[input]");
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.CommandText, Does.Contain("EXEC @moverLock = [other]]catalog].sys.sp_getapplock "));
            Assert.That(command.Parameters["MoverLockCatalog"].Value, Is.EqualTo("other]catalog"));
        }
    }

    [Test]
    public void Keeps_a_table_name_with_an_apostrophe_out_of_the_command_text()
    {
        var sqlConstants = new SqlServerConstants();
        var moveDueCommand = new MoveDueDelayedMessagesWithLockCommand(sqlConstants, new CanonicalQueueAddress("o'brien.delayed", "dbo", "nservicebus"), "[input]");
        using var command = new SqlCommand();

        moveDueCommand.Populate(command, 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Parameters["MoverLockResource"].Value, Is.EqualTo("[NSERVICEBUS].[DBO].[O'BRIEN.DELAYED]_MOVER"));
            Assert.That(command.CommandText, Does.Not.Contain("'[nservicebus].[dbo].[o'brien.delayed]"));
        }
    }

    [Test]
    public void Keeps_a_lock_resource_of_exactly_255_characters_unchanged()
    {
        // [c].[dbo].[table]_mover adds 18 characters to the catalog and table names
        var table = new string('t', 128);
        var catalog = new string('c', 255 - 18 - table.Length);
        var command = new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), new CanonicalQueueAddress(table, "dbo", catalog), "[input]");

        Assert.That(command.LockResource, Is.EqualTo($"[{catalog}].[dbo].[{table}]_mover".ToUpperInvariant()));
    }

    [Test]
    public void Hashes_a_lock_resource_longer_than_255_characters_to_255_characters()
    {
        var command = LongIdentifierCommand("first.Delayed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.LockResource, Has.Length.EqualTo(255));
            Assert.That(command.LockResource, Does.StartWith($"[{LongCatalog}].[{LongSchema}]".ToUpperInvariant()[..223]));
        }
    }

    [Test]
    public void Long_identifiers_that_differ_after_255_characters_get_different_lock_resources() =>
        Assert.That(AsSeenBySpGetAppLock(LongIdentifierCommand("first.Delayed")), Is.Not.EqualTo(AsSeenBySpGetAppLock(LongIdentifierCommand("second.Delayed"))));

    static string AsSeenBySpGetAppLock(MoveDueDelayedMessagesWithLockCommand command) =>
        command.LockResource.Length > 255 ? command.LockResource[..255] : command.LockResource;

    [Test]
    public void Long_identifiers_that_differ_only_in_case_get_the_same_lock_resource()
    {
        var lower = new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), new CanonicalQueueAddress("first.delayed", LongSchema, LongCatalog), "[input]");
        var upper = new MoveDueDelayedMessagesWithLockCommand(new SqlServerConstants(), new CanonicalQueueAddress("FIRST.DELAYED", LongSchema.ToUpperInvariant(), LongCatalog.ToUpperInvariant()), "[input]");

        Assert.That(upper.LockResource, Is.EqualTo(lower.LockResource));
    }

    // Every instance must derive the same resource, so the hash and format cannot change between versions
    [Test]
    public void Long_lock_resource_is_stable() =>
        Assert.That(LongIdentifierCommand("first.Delayed").LockResource[223..], Is.EqualTo("C2839D173974E80B31DC3318110BC1D9"));

    static MoveDueDelayedMessagesWithLockCommand LongIdentifierCommand(string table) =>
        new(new SqlServerConstants(), new CanonicalQueueAddress(table, LongSchema, LongCatalog), "[input]");

    static readonly string LongCatalog = new('c', 128);
    static readonly string LongSchema = new('s', 128);

    static readonly CanonicalQueueAddress delayedQueue = new("delayed", "dbo", "delayedcatalog");
}
