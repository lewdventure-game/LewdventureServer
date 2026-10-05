using Microsoft.Extensions.DependencyInjection;
using Server.Battles;
using Server.Bonuses;
using Server.Infrastructure.Analytics;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerStoreRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<TokenGenerator>();
            services.AddSingleton<UserRepository>();
            services.AddSingleton<PlayerProfileRepository>();
            services.AddSingleton<PlayerLedgerRepository>();
            services.AddSingleton<IdempotencyRepository>();
            services.AddSingleton<RunRepository>();
            services.AddSingleton<IMongoIndexContributor>(ResolveUserRepository);
            services.AddSingleton<IMongoIndexContributor>(ResolveLedgerRepository);
            services.AddSingleton<IMongoIndexContributor>(ResolveIdempotencyRepository);
            services.AddSingleton<IMongoIndexContributor>(ResolveRunRepository);
            services.AddSingleton<PlayerAnalytics>();
            services.AddSingleton<PlayerConfigVersionResolver>();
            services.AddSingleton<PlayerAuthService>();
            services.AddSingleton<PlayerProfileService>();
            services.AddSingleton<SummonProgressionRules>();
            services.AddSingleton<EquipmentProgressionRules>();
            services.AddSingleton<EquipmentMergeRules>();
            services.AddSingleton(CreateProgressionService);
            services.AddSingleton<PlayerDataService>();
            services.AddSingleton(CreateRewardApplier);
            services.AddSingleton(CreateRewardService);
            services.AddSingleton<ProgressionLimits>();
            services.AddSingleton(CreateResourceKeyCollector);
            services.AddSingleton<CheatPresetBuilder>();
            services.AddSingleton<CheatProfileEditor>();
            services.AddSingleton<CheatService>();
            services.AddSingleton<QaAccountService>();
            services.AddSingleton<QaTemplateRepository>();
            services.AddSingleton<QaTemplateService>();
            services.AddSingleton<QaDiagnosticsQueue>();
            services.AddSingleton<QaDiagnosticsRepository>();
            services.AddSingleton<IMongoIndexContributor>(ResolveQaDiagnosticsRepository);
        }

        private QaDiagnosticsRepository ResolveQaDiagnosticsRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<QaDiagnosticsRepository>();
        }

        private ResourceKeyCollector CreateResourceKeyCollector(IServiceProvider serviceProvider)
        {
            return new ResourceKeyCollector(CreateRewardParser(serviceProvider));
        }

        private PlayerProgressionService CreateProgressionService(IServiceProvider serviceProvider)
        {
            return new PlayerProgressionService(
                CreateRewardParser(serviceProvider),
                serviceProvider.GetRequiredService<EquipmentMergeRules>(),
                serviceProvider.GetRequiredService<EquipmentProgressionRules>(),
                serviceProvider.GetRequiredService<IdempotencyRepository>(),
                serviceProvider.GetRequiredService<ILogger<PlayerProgressionService>>(),
                serviceProvider.GetRequiredService<PlayerLedgerRepository>(),
                serviceProvider.GetRequiredService<PlayerProfileRepository>(),
                serviceProvider.GetRequiredService<PlayerProfileService>(),
                serviceProvider.GetRequiredService<RewardApplier>(),
                serviceProvider.GetRequiredService<SummonProgressionRules>(),
                serviceProvider.GetRequiredService<TimeProvider>());
        }

        private RewardApplier CreateRewardApplier(IServiceProvider serviceProvider)
        {
            return new RewardApplier(
                CreateRewardParser(serviceProvider),
                serviceProvider.GetRequiredService<IBonusWorkModeParser>(),
                serviceProvider.GetRequiredService<ILogger<RewardApplier>>());
        }

        private PlayerRewardService CreateRewardService(IServiceProvider serviceProvider)
        {
            return new PlayerRewardService(
                CreateRewardParser(serviceProvider),
                serviceProvider.GetRequiredService<IdempotencyRepository>(),
                serviceProvider.GetRequiredService<ILogger<PlayerRewardService>>(),
                serviceProvider.GetRequiredService<PlayerLedgerRepository>(),
                serviceProvider.GetRequiredService<PlayerProfileRepository>(),
                serviceProvider.GetRequiredService<PlayerProfileService>(),
                serviceProvider.GetRequiredService<RewardApplier>(),
                serviceProvider.GetRequiredService<TimeProvider>());
        }

        private IBattleRewardParser CreateRewardParser(IServiceProvider serviceProvider)
        {
            return new BattleRewardParser(serviceProvider.GetRequiredService<ICoreLog>());
        }

        private UserRepository ResolveUserRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<UserRepository>();
        }

        private PlayerLedgerRepository ResolveLedgerRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<PlayerLedgerRepository>();
        }

        private RunRepository ResolveRunRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<RunRepository>();
        }

        private IdempotencyRepository ResolveIdempotencyRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IdempotencyRepository>();
        }
    }
}
