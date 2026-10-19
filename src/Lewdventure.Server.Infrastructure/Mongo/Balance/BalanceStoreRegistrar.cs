using Microsoft.Extensions.DependencyInjection;

namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceStoreRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<BalanceScenarioRepository>();
            services.AddSingleton<BalanceRunRepository>();
            services.AddSingleton<IMongoIndexContributor>(ResolveBalanceRunRepository);
        }

        private BalanceRunRepository ResolveBalanceRunRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BalanceRunRepository>();
        }
    }
}
