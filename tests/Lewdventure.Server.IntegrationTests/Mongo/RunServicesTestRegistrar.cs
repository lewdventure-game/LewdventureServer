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

            services.AddScoped<IBattleAttackService, BattleAttackService>();
            services.AddScoped<IBattleBonusService, BattleBonusService>();
            services.AddScoped<IBattleCommandFactory, BattleCommandFactory>();
            services.AddScoped<IBattleDamageMath, BattleDamageMath>();
            services.AddScoped<IBattleParameterParser, BattleParameterParser>();
            services.AddScoped<IBattlePerkSimulator, BattlePerkSimulator>();
            services.AddScoped<IBattleRewardParser, BattleRewardParser>();
            services.AddScoped<IBattleRewardService, BattleRewardService>();
            services.AddScoped<IBattleScriptBuilder, BattleScriptBuilder>();
            services.AddScoped<IBattleSimulationValidator, BattleSimulationValidator>();
            services.AddScoped<IBattleSkillSimulator, BattleSkillSimulator>();
            services.AddScoped<IBattleStatusSimulator, BattleStatusSimulator>();
            services.AddScoped<IBattleSummonSimulator, BattleSummonSimulator>();
            services.AddScoped<ICharacteristicBucketApplicator, CharacteristicBucketApplicator>();
            services.AddScoped<ICharacteristicCalculator, CharacteristicCalculator>();
            services.AddScoped<IPerkFactory, PerkFactory>();
            services.AddScoped<ISkillFactory, SkillFactory>();
            services.AddScoped<IStatusParametersParser, StatusParametersParser>();
            services.AddScoped<IUnitBucketsFactory, UnitBucketsFactory>();
            services.AddScoped<IUnitBonusGranter, UnitBonusGranter>();
            services.AddScoped<IUnitLoadoutBinder, UnitLoadoutBinder>();
            services.AddScoped<IUnitStateBuilder, UnitStateBuilder>();
            services.AddScoped<IBattleTurnPhase, StatusTurnPhase>();
            services.AddScoped<IBattleTurnPhase, PerkTurnPhase>();
            services.AddScoped<IBattleTurnPhase, SummonTurnPhase>();
            services.AddScoped<IBattleTurnPhase, MainUnitTurnPhase>();
            services.AddScoped<IReadOnlyList<IBattleTurnPhase>>(ResolveTurnPhases);
            services.AddScoped<BattleSimulatorService>();
            services.AddScoped(ResolveConfigDistributor);
        }

        private IReadOnlyList<IBattleTurnPhase> ResolveTurnPhases(IServiceProvider serviceProvider)
        {
            return new List<IBattleTurnPhase>(serviceProvider.GetServices<IBattleTurnPhase>());
        }

        private IConfigDistributor ResolveConfigDistributor(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IGameConfigSetProvider>().Current.Distributor;
        }
    }
}
