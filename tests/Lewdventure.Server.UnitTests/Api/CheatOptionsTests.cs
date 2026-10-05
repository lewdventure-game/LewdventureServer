using Microsoft.Extensions.Configuration;
using Server.Api.Options;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class CheatOptionsTests
    {
        [Test]
        public void Validate_EnabledInProduction_Fails()
        {
            var result = new CheatOptionsValidator(new TestHostEnvironment("Production")).Validate(null, new CheatOptions { Enabled = true });

            Assert.That(result.Failed, Is.True);
        }

        [TestCase("Local")]
        [TestCase("Development")]
        [TestCase("Staging")]
        public void Validate_EnabledOutsideProduction_Succeeds(string environmentName)
        {
            var result = new CheatOptionsValidator(new TestHostEnvironment(environmentName)).Validate(null, new CheatOptions { Enabled = true });

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_DisabledInProduction_Succeeds()
        {
            var result = new CheatOptionsValidator(new TestHostEnvironment("Production")).Validate(null, new CheatOptions());

            Assert.That(result.Succeeded, Is.True);
        }

        [TestCase("Local", true)]
        [TestCase("Development", true)]
        [TestCase("Staging", true)]
        [TestCase("Production", false)]
        public void AppSettings_CheatsAndDiagnosticsPerEnvironment(string environmentName, bool expectedEnabled)
        {
            var directory = Path.GetDirectoryName(new ApiDirectoryLocator().FindFixture())!;
            var apiDirectory = Path.GetFullPath(Path.Combine(directory, "..", "..", "..", "..", "src", "Lewdventure.Server.Api"));
            var configuration = new ConfigurationBuilder()
                .SetBasePath(apiDirectory)
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{environmentName}.json", false)
                .Build();
            var options = new CheatOptions();

            var diagnostics = new QaDiagnosticsOptions();

            configuration.GetSection(CheatOptions.SectionName).Bind(options);
            configuration.GetSection(QaDiagnosticsOptions.SectionName).Bind(diagnostics);

            Assert.That(options.Enabled, Is.EqualTo(expectedEnabled));
            Assert.That(diagnostics.Enabled, Is.EqualTo(expectedEnabled));
        }
    }
}
