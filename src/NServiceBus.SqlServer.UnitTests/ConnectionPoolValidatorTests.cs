namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Transport.SQLServer;

    [TestFixture]
    public class ConnectionPoolValidatorTests
    {
        [Test]
        public void Is_not_validated_when_connection_pooling_not_specified()
        {
            var valid = ConnectionPoolValidator.Validate("Initial Catalog = xxx");

            Assert.IsFalse(valid);
        }

        [Test]
        public void Is_validated_when_both_min_and_max_pool_size_is_specified()
        {
            var valid = ConnectionPoolValidator.Validate("Initial Catalog = xxx; min pool size = 20; max pool size=120");

            Assert.IsTrue(valid);
        }

        [Test]
        public void Is_not_validated_when_only_min_pool_size_is_specified()
        {
            var valid = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Min Pool Size = 20;");

            Assert.IsFalse(valid);
        }

        [Test]
        public void Is_not_validated_when_only_max_pool_size_is_specified()
        {
            var valid = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Max Pool Size = 200");

            Assert.IsFalse(valid);
        }

        [Test]
        public void Is_not_validated_when_pooling_is_disabled()
        {
            var valid = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Min Pool Size = 20; Max Pool Size = 200; Pooling = false");

            Assert.IsFalse(valid);
        }

        [Test]
        public void Parses_pool_disable_values_with_yes_or_no()
        {
            var valid = ConnectionPoolValidator.Validate("Initial Catalog = xxx; Pooling = no");

            Assert.IsFalse(valid);
        }
    }
}