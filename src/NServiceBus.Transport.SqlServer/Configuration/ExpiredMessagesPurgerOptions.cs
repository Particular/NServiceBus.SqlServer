namespace NServiceBus
{
    /// <summary>
    /// Expired messages purger options.
    /// </summary>
    public class ExpiredMessagesPurgerOptions
    {
        internal ExpiredMessagesPurgerOptions() { }

        /// <summary>
        /// Instructs the transport to purge all expired messages from the input queue before starting the processing.
        /// </summary>
        public bool PurgeOnStartup { get; set; } = false;

        /// <summary>
        /// Maximum number of messages used in each delete batch when message purging on startup is enabled.
        /// </summary>
        public int? PurgeBatchSize { get; set; } = null;
    }
}