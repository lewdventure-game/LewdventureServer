using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Server;
using Server.Services;

namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenWebApplicationFactory : WebApplicationFactory<Program>
    {
        private const string ContentRootVariablePrefix = "ASPNETCORE_TEST_CONTENTROOT_";

        public GoldenWebApplicationFactory(GoldenPaths goldenPaths)
        {
            var assemblyName = typeof(Program).Assembly.GetName().Name ?? string.Empty;
            var variableName = ContentRootVariablePrefix + assemblyName.ToUpperInvariant().Replace(".", "_");

            Environment.SetEnvironmentVariable(variableName, goldenPaths.ServerProjectDirectory);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(ConfigureLogging);
            builder.ConfigureTestServices(ConfigureTestServices);
        }

        private void ConfigureLogging(ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();
        }

        private void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IGameConfigService>();
            services.AddSingleton<GoldenPaths>();
            services.AddSingleton<ConfigSnapshotLoader>();
            services.AddSingleton<ConfigDistributorFiller>();
            services.AddSingleton<IGameConfigService, FixtureGameConfigService>();
        }
    }
}
