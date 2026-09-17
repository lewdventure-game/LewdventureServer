using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Server.Infrastructure.Mongo;
using Testcontainers.MongoDb;

namespace Tests.Integration.Mongo
{
    internal sealed class MongoTestEnvironment : IAsyncDisposable
    {
        private readonly IntegrationTestGuard _guard = new();

        private MongoDbContainer? _container;
        private ServiceProvider? _provider;

        public string ConnectionString { get; private set; } = string.Empty;

        public string DatabaseName { get; private set; } = string.Empty;

        public IServiceProvider Services => _provider!;

        public async Task StartAsync(Action<IServiceCollection>? configureServices = null)
        {
            _guard.EnsureEnabled();

            ConnectionString = _guard.GetExternalConnectionString();

            if (string.IsNullOrEmpty(ConnectionString))
            {
                _container = new MongoDbBuilder("mongo:8.0").WithReplicaSet("rs0").Build();

                await _container.StartAsync();

                ConnectionString = _container.GetConnectionString();
            }

            _guard.EnsureLocal(ConnectionString);

            DatabaseName = _guard.CreateDatabaseName();

            var services = new ServiceCollection();

            services.AddLogging(builder => builder.ClearProviders());
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new MongoOptions
            {
                Enabled = true,
                ConnectionString = ConnectionString,
                DatabaseName = DatabaseName,
                ServerSelectionTimeoutSeconds = 10,
                RequireReplicaSet = true,
                ApplyIndexesOnStartup = true,
            }));

            new MongoServicesRegistrar().Register(services);

            configureServices?.Invoke(services);

            _provider = services.BuildServiceProvider();
        }

        public async ValueTask DisposeAsync()
        {
            if (_provider != null)
            {
                if (_guard.IsDisposableDatabase(DatabaseName))
                    await _provider.GetRequiredService<IMongoClient>().DropDatabaseAsync(DatabaseName);

                await _provider.DisposeAsync();
            }

            if (_container != null)
                await _container.DisposeAsync();
        }
    }
}
