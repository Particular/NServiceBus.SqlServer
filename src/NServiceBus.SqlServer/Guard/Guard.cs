namespace NServiceBus.Transports.SQLServer
{
    using System;
    using JetBrains.Annotations;

    static class Guard
    {
        [ContractAnnotation("value: null => halt")]
        public static void AgainstNullAndEmpty([InvokerParameterName] string argumentName, [NotNull] string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(argumentName);
            }
        }
    }
}
