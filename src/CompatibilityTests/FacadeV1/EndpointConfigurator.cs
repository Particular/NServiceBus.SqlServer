//namespace SqlServerV1
//{
//    using log4net.Appender;
//    using log4net.Core;
//    using log4net.Layout;
//    using NServiceBus;
//    using NServiceBus.Features;
//    using TransportCompatibilityTests.Common;
//    using TransportCompatibilityTests.Common.Messages;

//    class EndpointConfigurator
//    {
//        public static Configure Configure(EndpointInfo endpointInfo)
//        {
//            var configure = NServiceBus.Configure.With();
//            configure.DefaultBuilder();

//            configure.DefineEndpointName(endpointInfo.EndpointName);
//            Address.InitializeLocalAddress(endpointInfo.EndpointName);

//            configure.DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages") && t != typeof(TestEvent));
//            configure.DefiningEventsAs(t => t == typeof(TestEvent));

//            configure.UseInMemoryTimeoutPersister();
//            configure.InMemorySubscriptionStorage();
//            configure.UseTransport<SqlServer>();
//            configure.CustomConfigurationSource(new CustomConfiguration(endpointInfo.MessageMappings));

//            Feature.Enable<MessageDrivenSubscriptions>();

//            ConfigureLog4Net(endpointInfo.EndpointName);

//            return configure;
//        }

//        private static void ConfigureLog4Net(string endpointName)
//        {
//            var layout = new PatternLayout
//            {
//                ConversionPattern = "%d [%t] %-5p %c [%x] - %m%n"
//            };
//            layout.ActivateOptions();
//            var consoleAppender = new ColoredConsoleAppender
//            {
//                Threshold = Level.Debug,
//                Layout = layout
//            };
//            SetLoggingLibrary.Log4Net(null, consoleAppender);

//            var appender = new RollingFileAppender
//            {
//                DatePattern = endpointName + "_yyyy-MM-dd'.txt'",
//                RollingStyle = RollingFileAppender.RollingMode.Composite,
//                MaxFileSize = 10 * 1024 * 1024,
//                MaxSizeRollBackups = 10,
//                LockingModel = new FileAppender.MinimalLock(),
//                StaticLogFileName = false,
//                File = @"nsb_log_",
//                Layout = layout,
//                AppendToFile = true,
//                Threshold = Level.Debug,
//            };
//            SetLoggingLibrary.Log4Net(null, appender);
//        }
//    }
//}
