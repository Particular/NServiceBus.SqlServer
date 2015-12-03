using System;

namespace NServiceBus.Transports.SQLServer
{
    using JetBrains.Annotations;

    class Guard
    {
        [ContractAnnotation("value: null => halt")]
        public static void AgainstNull([InvokerParameterName] string argumentName, [NotNull] object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }

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