using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton<IValidateOptions<MongoOptions>, MongoOptionsValidator>();
            services.AddSingleton<MongoConventionsRegistrar>();
            services.AddSingleton<MongoClientFactory>();
            services.AddSingleton(CreateClient);
            services.AddSingleton<IMongoDatabaseAccessor, MongoDatabaseAccessor>();
            services.AddSingleton<IMongoTransactionRunner, MongoTransactionRunner>();
            services.AddSingleton<MongoTopologyValidator>();
            services.AddSingleton<MongoIndexBootstrapper>();
            services.AddSingleton<MongoStartupInitializer>();
        }

        private IMongoClient CreateClient(IServiceProvider serviceProvider)
        {
            serviceProvider.GetRequiredService<MongoConventionsRegistrar>().Register();

            var options = serviceProvider.GetRequiredService<IOptions<MongoOptions>>().Value;

            return serviceProvider.GetRequiredService<MongoClientFactory>().Create(options);
        }
    }
}
