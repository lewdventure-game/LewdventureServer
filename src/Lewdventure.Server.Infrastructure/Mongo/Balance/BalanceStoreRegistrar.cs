using Microsoft.Extensions.DependencyInjection;

namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceStoreRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<BalanceScenarioRepository>();
            services.AddSingleton<BalanceRunRepository>();
            services.AddSingleton<BalanceJobRepository>();
            services.AddSingleton<IMongoIndexContributor>(ResolveBalanceRunRepository);
            services.AddSingleton<IMongoIndexContributor>(ResolveBalanceJobRepository);
        }

        private BalanceJobRepository ResolveBalanceJobRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BalanceJobRepository>();
        }

        private BalanceRunRepository ResolveBalanceRunRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BalanceRunRepository>();
        }
    }
}
