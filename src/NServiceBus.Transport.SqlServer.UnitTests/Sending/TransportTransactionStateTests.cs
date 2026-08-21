namespace NServiceBus.Transport.SqlServer.UnitTests.Sending;

using System;
using System.Data;
using System.Data.Common;
using System.Transactions;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;
using Transport;
using static NServiceBus.Transport.Sql.Shared.SqlTransportTransactionState;

[TestFixture]
public class TransportTransactionStateTests
{
    [Test]
    public void Empty_transaction_is_outside_handler()
    {
        var transportTransaction = new TransportTransaction();

        Assert.That(transportTransaction.State, Is.SameAs(OutsideHandler.Instance));
    }

    [Test]
    public void No_transaction_carries_the_receive_connection()
    {
        var connection = new FakeDbConnection();

        var state = TransportTransactions.NoTransaction(connection).State;

        Assert.That(state, Is.InstanceOf<NoTransaction>());
        Assert.That(((NoTransaction)state).Connection, Is.SameAs(connection));
    }

    [Test]
    public void Receive_only_carries_no_data()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection);

        var transportTransaction = TransportTransactions.ReceiveOnly(connection, transaction);

        // the dispatcher opens its own connection for this state, so there is nothing to carry
        Assert.That(transportTransaction.State, Is.SameAs(ReceiveOnly.Instance));
    }

    [Test]
    public void Receive_only_still_writes_the_well_known_entries_for_downstream_components()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection);

        var transportTransaction = TransportTransactions.ReceiveOnly(connection, transaction);

        Assert.Multiple(() =>
        {
            Assert.That(transportTransaction.TryGet(TransportTransactionKeys.ReceiveOnlyTransactionMode, out bool receiveOnly) && receiveOnly, Is.True);
            Assert.That(transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection storedConnection) ? storedConnection : null, Is.SameAs(connection));
            Assert.That(transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction storedTransaction) ? storedTransaction : null, Is.SameAs(transaction));
        });
    }

    [Test]
    public void Sends_atomic_with_receive_carries_the_receive_connection_and_transaction()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection);

        var state = TransportTransactions.SendsAtomicWithReceive(connection, transaction).State;

        Assert.That(state, Is.InstanceOf<SendsAtomicWithReceive>());

        var sendsAtomicWithReceive = (SendsAtomicWithReceive)state;

        Assert.Multiple(() =>
        {
            Assert.That(sendsAtomicWithReceive.Connection, Is.SameAs(connection));
            Assert.That(sendsAtomicWithReceive.NativeTransaction, Is.SameAs(transaction));
        });
    }

    [Test]
    public void Transaction_scope_carries_no_data()
    {
        using var ambientTransaction = new CommittableTransaction();

        var transportTransaction = TransportTransactions.TransactionScope(ambientTransaction);

        // a new connection enlists in the ambient transaction automatically, so there is nothing to carry
        Assert.That(transportTransaction.State, Is.SameAs(AmbientTransaction.Instance));
    }

    [Test]
    public void User_provided_connection_has_no_native_transaction()
    {
        var connection = new FakeDbConnection();

        var state = TransportTransactions.UserProvided(connection).State;

        Assert.That(state, Is.InstanceOf<UserProvided>());

        var userProvided = (UserProvided)state;

        Assert.Multiple(() =>
        {
            Assert.That(userProvided.Connection, Is.SameAs(connection));
            Assert.That(userProvided.NativeTransaction, Is.Null);
        });
    }

    [Test]
    public void User_provided_transaction_falls_back_to_the_connection_it_was_created_on()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection);

        var state = TransportTransactions.UserProvided(transaction).State;

        Assert.That(state, Is.InstanceOf<UserProvided>());

        var userProvided = (UserProvided)state;

        Assert.Multiple(() =>
        {
            Assert.That(userProvided.Connection, Is.SameAs(connection));
            Assert.That(userProvided.NativeTransaction, Is.SameAs(transaction));
        });
    }

    [Test]
    public void User_provided_transaction_without_a_connection_throws()
    {
        var transportTransaction = TransportTransactions.UserProvided(new FakeDbTransaction(null));

        var exception = Assert.Throws<Exception>(() => _ = transportTransaction.State);

        Assert.That(exception.Message, Does.Contain("contains no SqlTransaction or SqlConnection"));
    }

    [Test]
    public void Hand_rolled_transaction_with_only_a_connection_is_no_transaction()
    {
        var connection = new FakeDbConnection();

        var transportTransaction = new TransportTransaction();
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);

        var state = transportTransaction.State;

        Assert.That(state, Is.InstanceOf<NoTransaction>());
        Assert.That(((NoTransaction)state).Connection, Is.SameAs(connection));
    }

    [Test]
    public void Hand_rolled_transaction_with_a_connection_and_a_transaction_is_sends_atomic_with_receive()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection);

        var transportTransaction = new TransportTransaction();
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        Assert.That(transportTransaction.State, Is.InstanceOf<SendsAtomicWithReceive>());
    }

    [Test]
    public void Hand_rolled_transaction_with_only_a_transaction_throws()
    {
        var transportTransaction = new TransportTransaction();
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, new FakeDbTransaction(new FakeDbConnection()));

        Assert.Throws<Exception>(() => _ = transportTransaction.State);
    }

    [Test]
    public void State_follows_the_entries_when_they_change_after_creation()
    {
        var connection = new FakeDbConnection();
        var transaction = new FakeDbTransaction(connection);

        var transportTransaction = TransportTransactions.NoTransaction(connection);

        Assert.That(transportTransaction.State, Is.InstanceOf<NoTransaction>());

        // the state is derived from the entries rather than stored alongside them, so a downstream
        // component adding a transaction cannot leave the dispatcher acting on a stale state
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        Assert.That(transportTransaction.State, Is.InstanceOf<SendsAtomicWithReceive>());
    }

    class FakeDbConnection : DbConnection
    {
        public override string ConnectionString { get; set; }
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() => throw new NotSupportedException();
        public override void Open() => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    class FakeDbTransaction(DbConnection connection) : DbTransaction
    {
        public override System.Data.IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection { get; } = connection;

        public override void Commit() => throw new NotSupportedException();
        public override void Rollback() => throw new NotSupportedException();
    }
}
