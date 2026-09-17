using Server.Api.Options;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class ServerOptionsValidatorTests
    {
        [Test]
        public void Validate_DefaultOptions_Succeeds()
        {
            var validator = new ServerOptionsValidator(new TestHostEnvironment("Production"));

            var result = validator.Validate(null, new ServerOptions());

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_SamePorts_Fails()
        {
            var validator = new ServerOptionsValidator(new TestHostEnvironment("Local"));
            var options = new ServerOptions { PublicPort = 5000, OpsPort = 5000 };

            var result = validator.Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }

        [Test]
        public void Validate_SwaggerInProduction_Fails()
        {
            var validator = new ServerOptionsValidator(new TestHostEnvironment("Production"));
            var options = new ServerOptions { EnableSwagger = true };

            var result = validator.Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }

        [Test]
        public void Validate_SwaggerInDevelopment_Succeeds()
        {
            var validator = new ServerOptionsValidator(new TestHostEnvironment("Development"));
            var options = new ServerOptions { EnableSwagger = true };

            var result = validator.Validate(null, options);

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_UnknownBindAddress_Fails()
        {
            var validator = new ServerOptionsValidator(new TestHostEnvironment("Local"));
            var options = new ServerOptions { BindAddress = BindAddressType.Unknown };

            var result = validator.Validate(null, options);

            Assert.That(result.Failed, Is.True);
        }
    }
}
