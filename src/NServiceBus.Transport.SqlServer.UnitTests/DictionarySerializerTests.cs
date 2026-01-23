namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using System.Collections.Generic;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using Particular.Approvals;

    [TestFixture]
    public class DictionarySerializerTests
    {

        [Test]
        public void Can_round_trip()
        {
            var before = new Dictionary<string, string>
            {
                {"key1", "value1"},
                {"key2", "value2"},
                {"keyWithNull", null},
                {"keyWithEmpty", ""}
            };
            var serialize = DictionarySerializer.Serialize(before);
            Approver.Verify(serialize);

            var after = DictionarySerializer.DeSerialize(serialize);

            AssertDictionariesAreTheSame(before, after);
        }

        void AssertDictionariesAreTheSame(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            foreach (var beforeItem in before)
            {
                Assert.That(beforeItem.Value, Is.EqualTo(after[beforeItem.Key]));
            }
            Assert.That(before.Count, Is.EqualTo(after.Count));
        }
    }
}
