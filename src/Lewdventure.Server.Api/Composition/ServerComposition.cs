using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;
using Server.Api.Endpoints;
using Server.Api.Health;
using Server.Api.Hosting;
using Server.Api.Http;
using Server.Api.Json;
using Server.Api.Options;
using Server.Api.Security;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.GoogleSheets;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo;
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
            RegisterMongo(services);

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

            services.AddOptions<GameConfigOptions>()
                .Bind(_configuration.GetSection(GameConfigOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<GoogleSheetsOptions>()
                .Bind(_configuration.GetSection(GoogleSheetsOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();
            services.AddSingleton<IValidateOptions<GoogleSheetsOptions>, GoogleSheetsOptionsValidator>();
            services.AddSingleton<IValidateOptions<GameConfigOptions>, GameConfigOptionsValidator>();
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

        private void RegisterMongo(IServiceCollection services)
        {
            services.AddOptions<MongoOptions>()
                .Bind(_configuration.GetSection(MongoOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var mongoOptions = new MongoOptions();

            _configuration.GetSection(MongoOptions.SectionName).Bind(mongoOptions);

            if (mongoOptions.Enabled == false)
            {
                services.AddSingleton<IValidateOptions<MongoOptions>, MongoOptionsValidator>();

                return;
            }

            new MongoServicesRegistrar().Register(services);
            new ConfigSnapshotStoreRegistrar().Register(services);

            services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongo", tags: new[] { HealthTags.Ready });

            var gameConfigOptions = new GameConfigOptions();

            _configuration.GetSection(GameConfigOptions.SectionName).Bind(gameConfigOptions);

            if (gameConfigOptions.Source == GameConfigSourceType.Mongo && gameConfigOptions.ReloadMode == GameConfigReloadMode.Poll)
                services.AddHostedService<ActiveConfigWatcher>();
        }

        private IConfigDistributor ResolveConfigDistributor(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
        }

        private void RegisterConfigs(IServiceCollection services)
        {
            services
                .AddSingleton<ConfigDomainNames>()
                .AddSingleton<ConfigSnapshotHasher>()
                .AddSingleton<ConfigSnapshotSerializer>()
                .AddSingleton<ConfigSnapshotValidator>()
                .AddSingleton<ConfigSnapshotDiff>()
                .AddSingleton<ConfigRowsParser>()
                .AddSingleton<FileConfigSnapshotSource>()
                .AddSingleton<GameConfigSetBuilder>()
                .AddSingleton<IGameConfigSetProvider, GameConfigSetProvider>()
                .AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>()
                .AddSingleton<GoogleCredentialProvider>()
                .AddSingleton<GoogleSheetsConfigImporter>()
                .AddSingleton<IGameConfigService, GameConfigService>()
                .AddSingleton<GameConfigReadyFilter>()
                .AddSingleton<ConfigResponseFactory>()
                .AddScoped(ResolveConfigDistributor);
        }
    }
}
