using Microsoft.Extensions.DependencyInjection;
using Server.Infrastructure.Experiments;

namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentStoreRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<ExperimentRepository>();
            services.AddSingleton<ExperimentChangeRepository>();
            services.AddSingleton<IMongoIndexContributor>(ResolveExperimentRepository);
            services.AddSingleton<IMongoIndexContributor>(ResolveExperimentChangeRepository);
            services.AddSingleton<ExperimentRegistry>();
            services.AddSingleton<ExperimentRegistryLoader>();
            services.AddSingleton<ExperimentGroupPicker>();
            services.AddSingleton<ExperimentAllocationValidator>();
            services.AddSingleton<ExperimentAssignmentService>();
            services.AddSingleton<ExperimentService>();
        }

        private ExperimentRepository ResolveExperimentRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<ExperimentRepository>();
        }

        private ExperimentChangeRepository ResolveExperimentChangeRepository(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<ExperimentChangeRepository>();
        }
    }
}
