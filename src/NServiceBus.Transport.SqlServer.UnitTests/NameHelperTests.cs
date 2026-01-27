namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using NUnit.Framework;
    using SqlServer;

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
            var quoteResult = SqlServerNameHelper.Quote(unquoted);
            var unquoteResult = SqlServerNameHelper.Unquote(quoted);

            Assert.That(unquoteResult, Is.EqualTo(unquoted));
            Assert.That(quoteResult, Is.EqualTo(quoted));
        }

        [Test]
        [TestCase("abc", "abc")]
        public void It_unquotes(string quoted, string unquoted)
        {
            var unquoteResult = SqlServerNameHelper.Unquote(quoted);

            Assert.That(unquoteResult, Is.EqualTo(unquoted));
        }
    }
}
