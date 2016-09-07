namespace NServiceBus.Transport.SQLServer
{
    class ValidationCheckResult
    {
        ValidationCheckResult(bool valid, string message)
        {
            IsValid = valid;
            Message = message;
        }

        public static ValidationCheckResult Valid()
        {
            return new ValidationCheckResult(true, null);
        }

        public static ValidationCheckResult Invalid(string message)
        {
            return new ValidationCheckResult(false, message);
        }

        public bool IsValid { get; private set; }
        public string Message { get; private set; }
    }
}