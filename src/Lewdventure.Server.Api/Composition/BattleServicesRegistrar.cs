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
                .AddScoped<IBattleParameterParser, BattleParameterParser>()
                .AddScoped<IBattlePerkSimulator, BattlePerkSimulator>()
                .AddScoped<IBattleRewardParser, BattleRewardParser>()
                .AddScoped<IBattleRewardService, BattleRewardService>()
                .AddScoped<IBattleScriptBuilder, BattleScriptBuilder>()
                .AddScoped<IBattleSimulationValidator, BattleSimulationValidator>()
                .AddScoped<IBattleSkillSimulator, BattleSkillSimulator>()
                .AddScoped<IBattleStatusSimulator, BattleStatusSimulator>()
                .AddScoped<IBattleSummonSimulator, BattleSummonSimulator>()
                .AddScoped<ICharacteristicCalculator, CharacteristicCalculator>()
                .AddScoped<IPerkFactory, PerkFactory>()
                .AddScoped<ISkillFactory, SkillFactory>()
                .AddScoped<IStatusParametersParser, StatusParametersParser>()
                .AddScoped<IUnitStateBuilder, UnitStateBuilder>()
                .AddScoped<BattleSimulatorService>();
        }
    }
}
