using Server.Battles;
using Server.Bonuses;
using Server.Infrastructure.Logging;
using Server.Services;

namespace Server.Api.Composition
{
    internal sealed class BattleServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services
                .AddScoped(CreateComposition)
                .AddScoped(ResolveSimulator)
                .AddScoped(ResolveValidator)
                .AddScoped(ResolveUnitStateBuilder)
                .AddScoped(ResolveRewardParser)
                .AddScoped(ResolveParameterParser)
                .AddScoped(ResolveScriptDigest);
        }

        private BattleComposition CreateComposition(IServiceProvider serviceProvider)
        {
            var coreLogFactory = serviceProvider.GetRequiredService<CoreLogFactory>();
            var coreLog = coreLogFactory.Create(serviceProvider.GetRequiredService<CoreLogCategories>().Battles);

            return new BattleComposition(serviceProvider.GetRequiredService<IConfigDistributor>(), coreLog);
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
    }
}
