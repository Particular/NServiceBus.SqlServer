namespace NServiceBus
{
    using System;

    partial class SqlServerTransportSettingsExtensions
    {
        /// <summary>
        /// Allows changing the queue peek delay.
        /// </summary>
        /// <param name="transportExtensions">The <see cref="TransportExtensions{T}" /> to extend.</param>
        /// <param name="delay">The delay value</param>
        [ObsoleteEx(Message = "WithPeekDelay has been obsoleted.", ReplacementTypeOrMember = "QueuePeekerOptions", RemoveInVersion = "8.0",
            TreatAsErrorFromVersion = "7.0")]
        public static TransportExtensions<SqlServerTransport> WithPeekDelay(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan? delay = null)
        {
            throw new NotImplementedException();
        }
    }
}
