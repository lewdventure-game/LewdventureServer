using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Infrastructure.GoogleSheets;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Tests.Integration.Mongo
{
    internal sealed class ConfigServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new GoogleSheetsOptions()));
            services.AddSingleton<ConfigDomainNames>();
            services.AddSingleton<ConfigSnapshotHasher>();
            services.AddSingleton<ConfigSnapshotSerializer>();
            services.AddSingleton<ConfigSnapshotValidator>();
            services.AddSingleton<ConfigSnapshotDiff>();
            services.AddSingleton<ConfigRowsParser>();
            services.AddSingleton<FileConfigSnapshotSource>();
            services.AddSingleton<GameConfigSetBuilder>();
            services.AddSingleton<IGameConfigSetProvider, GameConfigSetProvider>();
            services.AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>();
            services.AddSingleton<GoogleCredentialProvider>();
            services.AddSingleton<GoogleSheetsConfigImporter>();

            new ConfigSnapshotStoreRegistrar().Register(services);
        }
    }
}
