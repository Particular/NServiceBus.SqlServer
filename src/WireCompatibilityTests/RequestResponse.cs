namespace TestSuite
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NuGet.Versioning;
    using NUnit.Framework;
    using WireCompatibilityTests;

    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class RequestResponse
    {
        [OneTimeSetUp]
        public async Task SetUp()
        {
            await SqlHelper.CreateSchema(Global.ConnectionString, "receiver").ConfigureAwait(false);
            await SqlHelper.CreateSchema(Global.ConnectionString, "sender").ConfigureAwait(false);
        }

        [Test]
        [TestCaseSourcePackageSupportedVersions("NServiceBus.SqlServer", "[4,)")]
        public async Task SingleSchema(NuGetVersion senderVersion, NuGetVersion receiverVersion)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("Sender", "Receiver", senderVersion, receiverVersion, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            Assert.AreEqual(2, result.AuditedMessages.Values.Count, "Number of messages in audit queue");

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Send));
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Reply));

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);

            var requestVersion = SemanticVersion.Parse(request.Headers[Keys.WireCompatVersion]);
            var responseVersion = SemanticVersion.Parse(response.Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(senderVersion, requestVersion);
            Assert.AreEqual(receiverVersion, responseVersion);
        }

        [Test]
        [TestCaseSourcePackageSupportedVersions("NServiceBus.SqlServer", "[6,)")]
        public async Task MultiSchema(NuGetVersion senderVersion, NuGetVersion receiverVersion)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("SchemaSender", "SchemaReceiver", senderVersion, receiverVersion, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Send));
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Reply));

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);

            var requestVersion = SemanticVersion.Parse(request.Headers[Keys.WireCompatVersion]);
            var responseVersion = SemanticVersion.Parse(response.Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(senderVersion, requestVersion);
            Assert.AreEqual(receiverVersion, responseVersion);
        }
    }
}