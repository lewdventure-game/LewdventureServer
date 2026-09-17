using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Server.Api.Options;
using Server.Infrastructure.GoogleSheets;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class AppSettingsTests
    {
        [TestCase("Local", true, "Loopback")]
        [TestCase("Development", true, "Any")]
        [TestCase("Staging", false, "Any")]
        [TestCase("Production", false, "Any")]
        public void AppSettings_PerEnvironment_AreValid(string environmentName, bool expectedSwagger, string expectedBindAddress)
        {
            var configuration = BuildConfiguration(environmentName);
            var serverOptions = new ServerOptions();
            var googleSheetsOptions = new GoogleSheetsOptions();

            configuration.GetSection(ServerOptions.SectionName).Bind(serverOptions);
            configuration.GetSection(GoogleSheetsOptions.SectionName).Bind(googleSheetsOptions);

            var serverResult = new ServerOptionsValidator(new TestHostEnvironment(environmentName)).Validate(null, serverOptions);
            var sheetsResult = new GoogleSheetsOptionsValidator().Validate(null, googleSheetsOptions);

            Assert.That(serverResult.Succeeded, Is.True, serverResult.FailureMessage);
            Assert.That(sheetsResult.Succeeded, Is.True, sheetsResult.FailureMessage);
            Assert.That(serverOptions.EnableSwagger, Is.EqualTo(expectedSwagger));
            Assert.That(serverOptions.BindAddress.ToString(), Is.EqualTo(expectedBindAddress));
            Assert.That(serverOptions.PublicPort, Is.EqualTo(5000));
            Assert.That(serverOptions.OpsPort, Is.EqualTo(9090));
        }

        private IConfiguration BuildConfiguration(string environmentName)
        {
            var directory = FindApiDirectory();

            return new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{environmentName}.json", false)
                .Build();
        }

        private string FindApiDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "src", "Lewdventure.Server.Api");

                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                    return candidate;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("src/Lewdventure.Server.Api not found");
        }
    }
}
