namespace NServiceBus.SqlServer.UnitTests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using Particular.Approvals;
    using Transport.SQLServer;

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
            foreach (var beforeitem in before)
            {
                Assert.AreEqual(beforeitem.Value, after[beforeitem.Key]);
            }
            Assert.AreEqual(before.Count, after.Count);
        }
    }
}