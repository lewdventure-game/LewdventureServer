using Server.Api.Options;
using Server.Api.Security;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class AccessKeyTests
    {
        [Test]
        public void ApiKeyComparer_EqualKeys_ReturnsTrue()
        {
            Assert.That(new ApiKeyComparer().AreEqual("secret-value", "secret-value"), Is.True);
        }

        [TestCase("secret-valuE")]
        [TestCase("secret")]
        [TestCase("")]
        public void ApiKeyComparer_DifferentKeys_ReturnsFalse(string provided)
        {
            Assert.That(new ApiKeyComparer().AreEqual(provided, "secret-value"), Is.False);
        }

        [Test]
        public void ApiKeyComparer_EmptyExpected_ReturnsFalse()
        {
            Assert.That(new ApiKeyComparer().AreEqual(string.Empty, string.Empty), Is.False);
        }

        [Test]
        public void Validate_AdminShortKeyInStaging_Fails()
        {
            var validator = new AccessKeyOptionsValidator(new TestHostEnvironment("Staging"));

            var result = validator.Validate(null, new AdminOptions { Enabled = true, ApiKey = "short-key" });

            Assert.That(result.Failed, Is.True);
        }

        [Test]
        public void Validate_AdminShortKeyInLocal_Succeeds()
        {
            var validator = new AccessKeyOptionsValidator(new TestHostEnvironment("Local"));

            var result = validator.Validate(null, new AdminOptions { Enabled = true, ApiKey = "local-key" });

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_PublisherEnabledInProduction_Fails()
        {
            var validator = new AccessKeyOptionsValidator(new TestHostEnvironment("Production"));
            var options = new ConfigPublisherOptions { Enabled = true, ApiKey = new string('k', 40) };

            var result = validator.Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }

        [Test]
        public void Validate_DisabledPublisherWithoutKey_Succeeds()
        {
            var validator = new AccessKeyOptionsValidator(new TestHostEnvironment("Production"));

            var result = validator.Validate(null, new ConfigPublisherOptions());

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_ReverseProxyInvalidNetwork_Fails()
        {
            var options = new ReverseProxyOptions { Enabled = true, KnownNetworks = new List<string> { "not-a-network" } };

            var result = new ReverseProxyOptionsValidator().Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }
    }
}
