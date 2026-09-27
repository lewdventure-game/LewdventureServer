using Server.Battles;

namespace Server.Api.Composition
{
    internal sealed class BattleServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services
                .AddScoped<IBattleAttackService, BattleAttackService>()
                .AddScoped<IBattleBonusService, BattleBonusService>()
                .AddScoped<IBattleCommandFactory, BattleCommandFactory>()
                .AddScoped<IBattleDamageMath, BattleDamageMath>()
                .AddScoped<IBattleParameterParser, BattleParameterParser>()
                .AddScoped<IBattlePerkSimulator, BattlePerkSimulator>()
                .AddScoped<IBattleRewardParser, BattleRewardParser>()
                .AddScoped<IBattleRewardService, BattleRewardService>()
                .AddScoped<IBattleScriptBuilder, BattleScriptBuilder>()
                .AddScoped<IBattleSimulationValidator, BattleSimulationValidator>()
                .AddScoped<IBattleSkillSimulator, BattleSkillSimulator>()
                .AddScoped<IBattleStatusSimulator, BattleStatusSimulator>()
                .AddScoped<IBattleSummonSimulator, BattleSummonSimulator>()
                .AddScoped<ICharacteristicBucketApplicator, CharacteristicBucketApplicator>()
                .AddScoped<ICharacteristicCalculator, CharacteristicCalculator>()
                .AddScoped<IPerkFactory, PerkFactory>()
                .AddScoped<ISkillFactory, SkillFactory>()
                .AddScoped<IStatusParametersParser, StatusParametersParser>()
                .AddScoped<IUnitBucketsFactory, UnitBucketsFactory>()
                .AddScoped<IUnitBonusGranter, UnitBonusGranter>()
                .AddScoped<IUnitLoadoutBinder, UnitLoadoutBinder>()
                .AddScoped<IUnitStateBuilder, UnitStateBuilder>()
                .AddScoped<IBattleTurnPhase, StatusTurnPhase>()
                .AddScoped<IBattleTurnPhase, PerkTurnPhase>()
                .AddScoped<IBattleTurnPhase, SummonTurnPhase>()
                .AddScoped<IBattleTurnPhase, MainUnitTurnPhase>()
                .AddScoped<IReadOnlyList<IBattleTurnPhase>>(ResolveTurnPhases)
                .AddScoped<BattleSimulatorService>();
        }

        private IReadOnlyList<IBattleTurnPhase> ResolveTurnPhases(IServiceProvider serviceProvider)
        {
            return new List<IBattleTurnPhase>(serviceProvider.GetServices<IBattleTurnPhase>());
        }
    }
}
