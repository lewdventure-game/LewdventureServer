using Server.Logging;
using Microsoft.Extensions.DependencyInjection;
using Server.Bonuses;
using Server.GameConfigs;
using Server.Skills;
using Server.Infrastructure.Alerts;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Tests.Integration.Mongo
{
    internal sealed class ConfigServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<ConfigDomainNames>();
            services.AddSingleton<ConfigSnapshotHasher>();
            services.AddSingleton<ConfigSnapshotSerializer>();
            services.AddSingleton<EffectParameterRegistry>()
                .AddSingleton<EffectParametersValidator>()
                .AddSingleton<EnemyDataValidator>()
                .AddSingleton<SkillComponentRegistry>()
                .AddSingleton<SkillComponentValidator>()
                .AddSingleton<ConfigSnapshotValidator>();
            services.AddSingleton<ConfigSnapshotDiff>();
            services.AddSingleton<ConfigRangeReader>()
                .AddSingleton<ConfigRowLocator>();
            services.AddSingleton<ConfigRowsParser>();
            services.AddSingleton<SheetRowsConverter>();
            services.AddSingleton<UploadedSheetsSnapshotBuilder>();
            services.AddSingleton<FileConfigSnapshotSource>();
            services.AddSingleton<ICoreLog>(new SilentCoreLog());
            services.AddSingleton<GameConfigSetBuilder>();
            services.AddSingleton<IGameConfigSetProvider, GameConfigSetProvider>();
            services.AddSingleton<IBonusWorkModeParser, BonusWorkModeParser>();
            services.AddSingleton<IAlertPublisher, NullAlertPublisher>();

            new ConfigSnapshotStoreRegistrar().Register(services);
        }
    }
}
