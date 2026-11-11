namespace NServiceBus.Transport.Sql.Shared
{
    using System;

    abstract record MoveDueResult
    {
        MoveDueResult() { }

        public sealed record LockNotAcquired(TimeSpan RetryAfter) : MoveDueResult;

        public sealed record Moved(DateTime NextDue) : MoveDueResult;
    }
}
