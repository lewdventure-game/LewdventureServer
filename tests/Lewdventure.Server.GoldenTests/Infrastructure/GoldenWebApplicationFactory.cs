using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Server;

namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenWebApplicationFactory : WebApplicationFactory<Program>
    {
        private const string ContentRootVariablePrefix = "ASPNETCORE_TEST_CONTENTROOT_";

        private readonly GoldenPaths _goldenPaths;

        public GoldenWebApplicationFactory(GoldenPaths goldenPaths)
        {
            _goldenPaths = goldenPaths;

            var assemblyName = typeof(Program).Assembly.GetName().Name ?? string.Empty;
            var variableName = ContentRootVariablePrefix + assemblyName.ToUpperInvariant().Replace(".", "_");

            Environment.SetEnvironmentVariable(variableName, goldenPaths.ServerProjectDirectory);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("GameConfig:Source", "File");
            builder.UseSetting("GameConfig:FilePath", _goldenPaths.FixturePath);
            builder.UseSetting("GameConfig:FailStartupIfUnavailable", "true");
            builder.ConfigureLogging(ConfigureLogging);
        }

        private void ConfigureLogging(ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();
        }
    }
}
