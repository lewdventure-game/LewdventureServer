using Microsoft.Extensions.DependencyInjection;
using Server.Balance;

namespace Server.Runs
{
    internal sealed class RunServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<BalanceConfigDrafts>();
            services.AddSingleton<BalanceLevelSimulator>();
            services.AddSingleton<BalanceProgressService>();
            services.AddSingleton<BalanceService>();
            services.AddSingleton<BalanceSetupReader>();
            services.AddSingleton<BalanceSnapshotFactory>();
            services.AddSingleton<BalanceStatistics>();
            services.AddSingleton<BalanceSweepRunner>();
            services.AddSingleton<BattleScriptAnalyzer>();
            services.AddSingleton<RunAnalytics>();
            services.AddSingleton<RunCheatEditor>();
            services.AddSingleton<RunCheatService>();
            services.AddSingleton<RunEventKeys>();
            services.AddSingleton<RunRandomFactory>();
            services.AddSingleton<RunRecords>();
            services.AddSingleton<RunSnapshotBuilder>();
            services.AddSingleton<RunStageRoller>();
            services.AddScoped<RunEngine>();
            services.AddScoped<RunService>();
            services.AddScoped<PlayerCharacteristicsService>();
        }
    }
}
