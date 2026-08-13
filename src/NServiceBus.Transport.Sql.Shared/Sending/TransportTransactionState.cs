namespace NServiceBus.Transport.Sql.Shared;

/// <summary>
/// Describes the context in which a <see cref="Transport.TransportTransaction"/> was created, allowing the
/// dispatcher to determine how outgoing messages relate to the receive transaction. The state is derived
/// from the entries present in the transaction, which the transport records at creation time.
/// </summary>
enum TransportTransactionState
{
    /// <summary>Dispatch happens outside the context of an incoming message, e.g. from a send-only endpoint.</summary>
    OutsideHandler,

    /// <summary>The incoming message was received without a transaction. The receive connection can be reused but sends need their own transaction.</summary>
    NoTransaction,

    /// <summary>Sends must not take part in the receive transaction. Outgoing messages get a dedicated connection and transaction.</summary>
    ReceiveOnly,

    /// <summary>Outgoing messages take part in the receive connection and transaction.</summary>
    SendsAtomicWithReceive,

    /// <summary>An ambient transaction is active. New connections enlist in it automatically.</summary>
    TransactionScope,

    /// <summary>The user supplied their own connection or transaction through the send or publish options.</summary>
    UserProvided
}
