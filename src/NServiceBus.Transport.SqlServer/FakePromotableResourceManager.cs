namespace NServiceBus.Transport.SqlServer;

using System;
using System.Transactions;

class FakePromotableResourceManager : IEnlistmentNotification
{
    public static readonly Guid Id = Guid.NewGuid();
    public void Prepare(PreparingEnlistment preparingEnlistment) => preparingEnlistment.Prepared();
    public void Commit(Enlistment enlistment) => enlistment.Done();
    public void Rollback(Enlistment enlistment) => enlistment.Done();
    public void InDoubt(Enlistment enlistment) => enlistment.Done();

    public static void ForceDtc() => Transaction.Current.EnlistDurable(Id, new FakePromotableResourceManager(), EnlistmentOptions.None);
}