namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Transport.SqlServer;

    [TestFixture]
    public class NameHelperTests
    {
        [Test]
        [TestCase("abc", "[abc]")]
        [TestCase("a[bc", "[a[bc]")]
        [TestCase("a]bc", "[a]]bc]")]
        [TestCase("", "[]")]
        [TestCase(null, null)]
        public void It_quotes_and_unquotes(string unquoted, string quoted)
        {
            var quoteResult = NameHelper.Quote(unquoted);
            var unquoteResult = NameHelper.Unquote(quoted);

            Assert.AreEqual(unquoted, unquoteResult);
            Assert.AreEqual(quoted, quoteResult);
        }

        [Test]
        [TestCase("abc", "abc")]
        public void It_unquotes(string quoted, string unquoted)
        {
            var unquoteResult = NameHelper.Unquote(quoted);

            Assert.AreEqual(unquoted, unquoteResult);
        }
    }
}