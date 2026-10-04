using Microsoft.Extensions.DependencyInjection;
using Server.Infrastructure.Mongo.Experiments;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;

namespace Tests.Integration.Mongo
{
    internal sealed class PlayerServicesRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AuthOptions
            {
                Enabled = true,
                SigningKey = "integration-test-signing-key-0123456789",
                AccessTokenMinutes = 60,
                RefreshTokenDays = 90,
            }));
            services.AddSingleton(TimeProvider.System);

            new PlayerStoreRegistrar().Register(services);
            new ExperimentStoreRegistrar().Register(services);
            new ConfigServicesRegistrar().Register(services);
        }
    }
}
