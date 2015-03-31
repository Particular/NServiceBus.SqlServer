namespace NServiceBus.Features
{
    using System.Configuration;
    using System.Reflection;
    using NServiceBus.Config;
    using NServiceBus.Logging;
    using NServiceBus.Settings;

    static class ErrorQueueSettings
    {
        public static Address GetConfiguredErrorQueue(ReadOnlySettings settings)
        {
            var errorQueue = Address.Undefined;

            var section = settings.GetConfigSection<MessageForwardingInCaseOfFaultConfig>();
            if (section != null)
            {
                if (string.IsNullOrWhiteSpace(section.ErrorQueue))
                {
                    throw new ConfigurationErrorsException(
                        "'MessageForwardingInCaseOfFaultConfig' configuration section is found but 'ErrorQueue' value is missing." +
                        "\n The following is an example for adding such a value to your app config: " +
                        "\n <MessageForwardingInCaseOfFaultConfig ErrorQueue=\"error\"/> \n");
                }

                Logger.Debug("Error queue retrieved from <MessageForwardingInCaseOfFaultConfig> element in config file.");

                errorQueue = Address.Parse(section.ErrorQueue);
            }
            else
            {
                var regRederType = typeof(Address).Assembly.GetType("NServiceBus.Utils.RegistryReader", true);
                var readMethod = regRederType.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var registryErrorQueue = (string)readMethod.Invoke(null,new object[]{"ErrorQueue", null});
                if (!string.IsNullOrWhiteSpace(registryErrorQueue))
                {
                    Logger.Debug("Error queue retrieved from registry settings.");
                    errorQueue = Address.Parse(registryErrorQueue);
                }
            }

            if (errorQueue == Address.Undefined)
            {
                throw new ConfigurationErrorsException("Faults forwarding requires an error queue to be specified. Please add a 'MessageForwardingInCaseOfFaultConfig' section to your app.config" +
                                                       "\n or configure a global one using the powershell command: Set-NServiceBusLocalMachineSettings -ErrorQueue {address of error queue}");
            }

            return errorQueue;

        }

        static ILog Logger = LogManager.GetLogger(typeof(ErrorQueueSettings));
    }
}