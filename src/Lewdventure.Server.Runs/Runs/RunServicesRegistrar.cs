using Microsoft.Extensions.DependencyInjection;
using Server.Balance;

namespace Server.Runs
{
    internal sealed class RunServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<BalanceService>();
            services.AddSingleton<BalanceSnapshotFactory>();
            services.AddSingleton<BattleScriptAnalyzer>();
            services.AddSingleton<RunAnalytics>();
            services.AddSingleton<RunCheatEditor>();
            services.AddSingleton<RunCheatService>();
            services.AddSingleton<RunEventKeys>();
            services.AddSingleton<RunRandomFactory>();
            services.AddSingleton<RunSnapshotBuilder>();
            services.AddSingleton<RunStageRoller>();
            services.AddScoped<RunService>();
            services.AddScoped<PlayerCharacteristicsService>();
        }
    }
}
