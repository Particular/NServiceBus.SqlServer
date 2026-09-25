namespace NServiceBus.Transport.Sql.Shared
{
    /// <summary>
    /// What a <see cref="ProcessStrategy"/> did with the row its <see cref="ReceiveAttempt"/>
    /// received, so the attempt can keep the <see cref="ReceiveState"/> anchor in step with the queue.
    /// </summary>
    enum ProcessOutcome
    {
        NoMessage,
        Committed,
        RolledBack
    }
}
