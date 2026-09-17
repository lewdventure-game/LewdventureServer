using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoStartupInitializer
    {
        private readonly ILogger<MongoStartupInitializer> _logger;
        private readonly MongoClientFactory _mongoClientFactory;
        private readonly MongoIndexBootstrapper _mongoIndexBootstrapper;
        private readonly MongoOptions _mongoOptions;
        private readonly MongoTopologyValidator _mongoTopologyValidator;

        public MongoStartupInitializer(
            ILogger<MongoStartupInitializer> logger,
            MongoClientFactory mongoClientFactory,
            MongoIndexBootstrapper mongoIndexBootstrapper,
            IOptions<MongoOptions> mongoOptions,
            MongoTopologyValidator mongoTopologyValidator)
        {
            _logger = logger;
            _mongoClientFactory = mongoClientFactory;
            _mongoIndexBootstrapper = mongoIndexBootstrapper;
            _mongoOptions = mongoOptions.Value;
            _mongoTopologyValidator = mongoTopologyValidator;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var hosts = _mongoClientFactory.DescribeHosts(_mongoOptions.ConnectionString);
            var replicaSetName = await _mongoTopologyValidator.GetReplicaSetNameAsync(cancellationToken);

            _logger.LogInformation("[Mongo] connected hosts = {Hosts} database = {Database} replicaSet = {ReplicaSet}", hosts, _mongoOptions.DatabaseName, replicaSetName);

            if (_mongoOptions.RequireReplicaSet && string.IsNullOrEmpty(replicaSetName))
                throw new InvalidOperationException("MongoDB is not running as a replica set; transactions require replica set rs0.");

            if (_mongoOptions.ApplyIndexesOnStartup == false)
                return;

            var applied = await _mongoIndexBootstrapper.ApplyAsync(cancellationToken);

            _logger.LogInformation("[Mongo] indexes applied count = {Count}", applied);
        }
    }
}
