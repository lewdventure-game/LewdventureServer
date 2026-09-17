using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Server;
using Server.Services;

namespace Tests.Unit.Api
{
    internal sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        private const string ContentRootVariable = "ASPNETCORE_TEST_CONTENTROOT_LEWDVENTURE_SERVER_API";

        private readonly Dictionary<string, string> _settings;

        public ApiWebApplicationFactory(Dictionary<string, string> settings)
        {
            _settings = settings;

            Environment.SetEnvironmentVariable(ContentRootVariable, new ApiDirectoryLocator().Find());
        }

        public FakeGameConfigService ConfigService { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(ConfigureLogging);

            foreach (var pair in _settings)
                builder.UseSetting(pair.Key, pair.Value);

            builder.ConfigureTestServices(ConfigureTestServices);
        }

        private void ConfigureLogging(ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();
        }

        private void ConfigureTestServices(IServiceCollection services)
        {
            services.RemoveAll<IGameConfigService>();
            services.AddSingleton<IGameConfigService>(ConfigService);
        }
    }
}
