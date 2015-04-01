
namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.ObjectBuilder.Common;
    using NServiceBus.Pipeline;
    using NServiceBus.Transports.SQLServer.Config;

    abstract class ConfigTest
    {
        protected IBuilder Builder { get; private set; }
 
        protected IBuilder Activate(BusConfiguration busConfiguration, ConfigBase featureConfig, string connectionString = "")
        {
            var builder = new TestBuilder();
            busConfiguration.UseContainer(builder);
            var configure = (Configure)typeof(BusConfiguration).GetMethod("BuildConfiguration", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(busConfiguration, new object[0]);
            var featureContext = (FeatureConfigurationContext)typeof(FeatureConfigurationContext).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[]
            {
                typeof(Configure)
            }, new ParameterModifier[0]).Invoke(new object[]
            {
                configure
            });
            var settings = busConfiguration.GetSettings();
            featureConfig.SetUpDefaults(settings);
            featureConfig.Configure(featureContext, connectionString);

            builder.CallAllFactories();
            Builder = configure.Builder;
            return configure.Builder;
        }

        private class TestBuilder : IContainer
        {
            readonly Dictionary<Type, Func<object>> factoryMethods = new Dictionary<Type, Func<object>>();

            public void CallAllFactories()
            {
                foreach (var factoryMethod in factoryMethods.Values)
                {
                    factoryMethod();
                }
            }

            public void Dispose()
            {
            }

            public object Build(Type typeToBuild)
            {
                Func<object> factory;
                if (factoryMethods.TryGetValue(typeToBuild, out factory))
                {
                    return factory();
                }
                return null;
            }

            public IContainer BuildChildContainer()
            {
                throw new NotImplementedException();
            }

            public IEnumerable<object> BuildAll(Type typeToBuild)
            {
                throw new NotImplementedException();
            }

            public void Configure(Type component, DependencyLifecycle dependencyLifecycle)
            {
                //NOOP
            }

            public void Configure<T>(Func<T> component, DependencyLifecycle dependencyLifecycle)
            {
                factoryMethods[typeof(T)] = () => component();
            }

            public void ConfigureProperty(Type component, string property, object value)
            {
            }

            public void RegisterSingleton(Type lookupType, object instance)
            {
                factoryMethods[lookupType] = () => instance;
            }

            public bool HasComponent(Type componentType)
            {
                return factoryMethods.ContainsKey(componentType);
            }

            public void Release(object instance)
            {
            }
        }
    }
}
