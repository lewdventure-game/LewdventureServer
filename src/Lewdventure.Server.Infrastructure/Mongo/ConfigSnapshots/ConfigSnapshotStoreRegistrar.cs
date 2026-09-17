using Microsoft.Extensions.DependencyInjection;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigSnapshotStoreRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<ConfigSnapshotRepository>();
            services.AddSingleton<ConfigActivationRepository>();
            services.AddSingleton<IMongoIndexContributor>(ResolveSnapshotRepository);
            services.AddSingleton<IMongoIndexContributor>(ResolveActivationRepository);
            services.AddSingleton<ConfigPublishingService>();
        }

        private ConfigSnapshotRepository ResolveSnapshotRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<ConfigSnapshotRepository>();
        }

        private ConfigActivationRepository ResolveActivationRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<ConfigActivationRepository>();
        }
    }
}
