using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Server;

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

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("GameConfig:Source", "File");
            builder.UseSetting("GameConfig:FilePath", new ApiDirectoryLocator().FindFixture());
            builder.ConfigureLogging(ConfigureLogging);

            foreach (var pair in _settings)
                builder.UseSetting(pair.Key, pair.Value);

        }

        private void ConfigureLogging(ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();
        }
    }
}
