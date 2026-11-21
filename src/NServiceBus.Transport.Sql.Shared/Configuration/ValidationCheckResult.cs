namespace NServiceBus.Transport.Sql.Shared
{
    class ValidationCheckResult
    {
        ValidationCheckResult(bool valid, string message)
        {
            IsValid = valid;
            Message = message;
        }

        public static ValidationCheckResult Valid() => new(true, null);

        public static ValidationCheckResult Invalid(string message) => new(false, message);

        public bool IsValid { get; }
        public string Message { get; }
    }
}