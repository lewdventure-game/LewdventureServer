using Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Server.Battles;
using Server.GameConfigs;
using Server.Runs;
using Server.Services;

namespace Tests.Integration.Mongo
{
    internal sealed class RunServicesTestRegistrar
    {
        public void Register(IServiceCollection services)
        {
            new PlayerServicesRegistrar().Register(services);
            new RunServicesRegistrar().Register(services);

            services.AddScoped(CreateComposition);
            services.AddScoped(ResolveSimulator);
            services.AddScoped(ResolveValidator);
            services.AddScoped(ResolveUnitStateBuilder);
            services.AddScoped(ResolveRewardParser);
            services.AddScoped(ResolveParameterParser);
            services.AddScoped(ResolveScriptDigest);
            services.AddScoped(ResolveConfigDistributor);
        }

        private BattleComposition CreateComposition(IServiceProvider serviceProvider)
        {
            return new BattleComposition(serviceProvider.GetRequiredService<IConfigDistributor>(), new SilentCoreLog());
        }

        private IBattleSimulatorService ResolveSimulator(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BattleComposition>().BattleSimulatorService;
        }

        private IBattleSimulationValidator ResolveValidator(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BattleComposition>().BattleSimulationValidator;
        }

        private IUnitStateBuilder ResolveUnitStateBuilder(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BattleComposition>().UnitStateBuilder;
        }

        private IBattleScriptDigest ResolveScriptDigest(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BattleComposition>().BattleScriptDigest;
        }

        private IBattleParameterParser ResolveParameterParser(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BattleComposition>().BattleParameterParser;
        }

        private IBattleRewardParser ResolveRewardParser(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BattleComposition>().BattleRewardParser;
        }

        private IConfigDistributor ResolveConfigDistributor(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
        }
    }
}
