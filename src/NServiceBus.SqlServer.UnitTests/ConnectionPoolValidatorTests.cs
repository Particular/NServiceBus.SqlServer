﻿namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Transport.SQLServer;

    [TestFixture]
    public class ConnectionPoolValidatorTests
    {
        [Test]
        public void Is_not_validated_when_connection_pooling_not_specified()
        {
            var result = ConnectionPoolValidator.Validate("Initial Catalog = xxx");

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Is_validated_when_both_min_and_max_pool_size_is_specified()
        {
            var result = ConnectionPoolValidator.Validate("Initial Catalog = xxx; min pool size = 20; max pool size=120");

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Is_not_validated_when_only_min_pool_size_is_specified()
        {
            var result = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Min Pool Size = 20;");

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Is_not_validated_when_pooling_is_enabled_and_no_min_and_max_is_set()
        {
            var result = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Pooling = true");

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Is_validated_when_pooling_is_disabled()
        {
            var result = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Pooling = false");

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Parses_pool_disable_values_with_yes_or_no()
        {
            var result = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Pooling = no");

            Assert.IsTrue(result.IsValid);
        }
    }
}