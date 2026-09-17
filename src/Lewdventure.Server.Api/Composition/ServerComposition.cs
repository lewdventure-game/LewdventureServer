using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;
using Server.Api.Health;
using Server.Api.Hosting;
using Server.Api.Json;
using Server.Api.Options;
using Server.Api.Security;
using Server.Bonuses;
using Server.Infrastructure.GoogleSheets;
using Server.Services;

namespace Server.Api.Composition
{
    internal sealed class ServerComposition
    {
        private readonly IConfiguration _configuration;

        public ServerComposition(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Register(IServiceCollection services)
        {
            RegisterOptions(services);
            RegisterHosting(services);
            RegisterConfigs(services);

            new SecurityRegistrar(_configuration).Register(services);
            new BattleServicesRegistrar().Register(services);

            services.AddSingleton(new NewtonsoftSettingsFactory().Create());
        }

        private void RegisterOptions(IServiceCollection services)
        {
            services.AddOptions<ServerOptions>()
                .Bind(_configuration.GetSection(ServerOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<GoogleSheetsOptions>()
                .Bind(_configuration.GetSection(GoogleSheetsOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
            services.AddSingleton<IValidateOptions<GoogleSheetsOptions>, GoogleSheetsOptionsValidator>();
            services.AddOptions<HostOptions>().Configure<IOptions<ServerOptions>>(ConfigureHostOptions);
        }

        private void ConfigureHostOptions(HostOptions hostOptions, IOptions<ServerOptions> serverOptions)
        {
            hostOptions.ShutdownTimeout = TimeSpan.FromSeconds(serverOptions.Value.ShutdownTimeoutSeconds);
        }

        private void RegisterHosting(IServiceCollection services)
        {
            services.AddSingleton<BuildInfo>();
            services.AddHostedService<StartupBannerService>();
            services.AddHostedService<DangerousConfigurationReporter>();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddHttpLogging(ConfigureHttpLogging);

            services.AddHealthChecks()
                .AddCheck<SelfHealthCheck>("self", tags: new[] { HealthTags.Live })
                .AddCheck<GameConfigLoadedHealthCheck>("game-config", tags: new[] { HealthTags.Ready });
        }

        private void ConfigureHttpLogging(HttpLoggingOptions httpLoggingOptions)
        {
            httpLoggingOptions.LoggingFields = HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            httpLoggingOptions.CombineLogs = true;
        }

        private void RegisterConfigs(IServiceCollection services)
        {
            services
                .AddSingleton<IConfigDistributor, ConfigDistributor>()
                .AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>()
                .AddSingleton<GoogleCredentialProvider>()
                .AddSingleton<IGameConfigService, GameConfigService>();
        }
    }
}
