using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public UserRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<UserDocument> Collection => _mongoDatabaseAccessor.GetCollection<UserDocument>(MongoCollectionNames.Users);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var deviceKeys = Builders<BsonDocument>.IndexKeys.Ascending("devices.deviceIdHash");
            var deviceModel = new CreateIndexModel<BsonDocument>(deviceKeys, new CreateIndexOptions { Name = "devices.deviceIdHash_1", Unique = true, Sparse = true });
            var identityKeys = Builders<BsonDocument>.IndexKeys.Ascending("identities.provider").Ascending("identities.subject");
            var identityModel = new CreateIndexModel<BsonDocument>(identityKeys, new CreateIndexOptions { Name = "identities_1", Unique = true, Sparse = true });

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.Users, deviceModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, identityModel),
            };
        }

        public async Task<UserDocument?> GetAsync(string userId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<UserDocument>.Filter.Eq(item => item.Id, userId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDocument?> FindByDeviceAsync(string deviceIdHash, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.ElemMatch(item => item.Devices, device => device.DeviceIdHash == deviceIdHash);

            return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<long> DeleteAsync(string userId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteOneAsync(Builders<UserDocument>.Filter.Eq(item => item.Id, userId), cancellationToken);

            return result.DeletedCount;
        }

        public async Task InsertAsync(UserDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<bool> UpsertDeviceAsync(string userId, UserDeviceDocument device, DateTime now, CancellationToken cancellationToken)
        {
            var removeFilter = Builders<UserDocument>.Filter.Eq(item => item.Id, userId);
            var pull = Builders<UserDocument>.Update
                .PullFilter(item => item.Devices, existing => existing.DeviceIdHash == device.DeviceIdHash)
                .Set(item => item.UpdatedAt, now);

            await Collection.UpdateOneAsync(removeFilter, pull, cancellationToken: cancellationToken);

            var push = Builders<UserDocument>.Update
                .Push(item => item.Devices, device)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(removeFilter, push, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }
    }
}
