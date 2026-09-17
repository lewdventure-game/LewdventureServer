using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Server.Infrastructure.Mongo;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class MongoInfrastructureTests
    {
        private MongoTestEnvironment _environment = null!;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _environment = new MongoTestEnvironment();

            await _environment.StartAsync(services => services.AddSingleton<IMongoIndexContributor, TestIndexContributor>());
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _environment.DisposeAsync();
        }

        [Test]
        public async Task Startup_ReplicaSet_IsDetectedAndIndexesApplied()
        {
            var initializer = _environment.Services.GetRequiredService<MongoStartupInitializer>();

            await initializer.InitializeAsync(CancellationToken.None);
            await initializer.InitializeAsync(CancellationToken.None);

            var topology = _environment.Services.GetRequiredService<MongoTopologyValidator>();
            var accessor = _environment.Services.GetRequiredService<IMongoDatabaseAccessor>();
            var replicaSetName = await topology.GetReplicaSetNameAsync(CancellationToken.None);
            var indexes = await (await accessor.GetCollection<BsonDocument>(TestIndexContributor.CollectionName).Indexes.ListAsync()).ToListAsync();

            Assert.That(replicaSetName, Is.EqualTo("rs0"));
            Assert.That(indexes.Exists(index => index["name"] == "createdAt_1"), Is.True);
        }

        [Test]
        public async Task Transaction_Commit_PersistsAllWrites()
        {
            var runner = _environment.Services.GetRequiredService<IMongoTransactionRunner>();
            var accessor = _environment.Services.GetRequiredService<IMongoDatabaseAccessor>();
            var collection = accessor.GetCollection<BsonDocument>("it_commit");

            await accessor.Database.CreateCollectionAsync("it_commit");

            await runner.ExecuteAsync(async (session, token) =>
            {
                await collection.InsertOneAsync(session, new BsonDocument("_id", "a"), cancellationToken: token);
                await collection.InsertOneAsync(session, new BsonDocument("_id", "b"), cancellationToken: token);

                return true;
            }, CancellationToken.None);

            Assert.That(await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty), Is.EqualTo(2));
        }

        [Test]
        public async Task Transaction_Exception_RollsBackWrites()
        {
            var runner = _environment.Services.GetRequiredService<IMongoTransactionRunner>();
            var accessor = _environment.Services.GetRequiredService<IMongoDatabaseAccessor>();
            var collection = accessor.GetCollection<BsonDocument>("it_rollback");

            await accessor.Database.CreateCollectionAsync("it_rollback");

            Assert.ThrowsAsync<InvalidOperationException>(async () => await runner.ExecuteAsync<bool>(async (session, token) =>
            {
                await collection.InsertOneAsync(session, new BsonDocument("_id", "a"), cancellationToken: token);

                throw new InvalidOperationException("rollback");
            }, CancellationToken.None));

            Assert.That(await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty), Is.EqualTo(0));
        }

        [Test]
        public async Task HealthCheck_ReachableMongo_IsHealthy()
        {
            var accessor = _environment.Services.GetRequiredService<IMongoDatabaseAccessor>();
            var healthCheck = new MongoHealthCheck(accessor);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy), result.Description);
        }

        [Test]
        public void Guard_RemoteHost_IsRejected()
        {
            var guard = new IntegrationTestGuard();

            if (Environment.GetEnvironmentVariable(IntegrationTestGuard.AllowRemoteVariable) == "1")
                Assert.Ignore("remote hosts explicitly allowed");

            Assert.Throws<InvalidOperationException>(() => guard.EnsureLocal("mongodb://db.example.com:27017"));
            Assert.DoesNotThrow(() => guard.EnsureLocal("mongodb://127.0.0.1:27017"));
        }
    }
}
