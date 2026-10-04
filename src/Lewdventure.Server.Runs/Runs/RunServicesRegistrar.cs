using Microsoft.Extensions.DependencyInjection;

namespace Server.Runs
{
    internal sealed class RunServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<RunAnalytics>();
            services.AddSingleton<RunEventKeys>();
            services.AddSingleton<RunRandomFactory>();
            services.AddSingleton<RunSnapshotBuilder>();
            services.AddSingleton<RunStageRoller>();
            services.AddScoped<RunService>();
            services.AddScoped<PlayerCharacteristicsService>();
        }
    }
}
