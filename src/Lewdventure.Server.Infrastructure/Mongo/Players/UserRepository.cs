using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;
        private readonly IMongoTransactionRunner _mongoTransactionRunner;

        public UserRepository(IMongoDatabaseAccessor mongoDatabaseAccessor, IMongoTransactionRunner mongoTransactionRunner)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
            _mongoTransactionRunner = mongoTransactionRunner;
        }

        private IMongoCollection<UserDocument> Collection => _mongoDatabaseAccessor.GetCollection<UserDocument>(MongoCollectionNames.Users);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var deviceKeys = Builders<BsonDocument>.IndexKeys.Ascending("devices.deviceIdHash");
            var deviceModel = new CreateIndexModel<BsonDocument>(deviceKeys, new CreateIndexOptions { Name = "devices.deviceIdHash_1", Unique = true, Sparse = true });
            var identityKeys = Builders<BsonDocument>.IndexKeys.Ascending("identities.provider").Ascending("identities.subject");
            var identityModel = new CreateIndexModel<BsonDocument>(identityKeys, new CreateIndexOptions { Name = "identities_1", Unique = true, Sparse = true });
            var experimentKeys = Builders<BsonDocument>.IndexKeys.Ascending("experiment.experimentId").Ascending("experiment.groupId");
            var experimentModel = new CreateIndexModel<BsonDocument>(experimentKeys, new CreateIndexOptions { Name = "experiment_1", Sparse = true });
            var qaKeys = Builders<BsonDocument>.IndexKeys.Ascending("qa.alias");
            var qaModel = new CreateIndexModel<BsonDocument>(qaKeys, new CreateIndexOptions { Name = "qa.alias_1", Unique = true, Sparse = true });

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.Users, deviceModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, identityModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, experimentModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, qaModel),
            };
        }

        public async Task<UserExperimentDocument?> GetExperimentAsync(string userId, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<UserDocument>.Filter.Eq(item => item.Id, userId))
                .Project(item => item.Experiment)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDocument?> GetAnalyticsProjectionAsync(string userId, CancellationToken cancellationToken)
        {
            var projection = Builders<UserDocument>.Projection
                .Include(item => item.Experiment)
                .Include(item => item.LastCountry);

            return await Collection
                .Find(Builders<UserDocument>.Filter.Eq(item => item.Id, userId))
                .Project<UserDocument>(projection)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> UpdateExperimentAsync(
            string userId,
            UserExperimentDocument? expected,
            UserExperimentDocument? assignment,
            List<string> seenExperimentIds,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq(item => item.Id, userId),
                CreateExperimentFilter(expected));
            var update = Builders<UserDocument>.Update
                .AddToSetEach(item => item.ExperimentsSeen, seenExperimentIds)
                .Set(item => item.UpdatedAt, now);

            if (assignment != null)
                update = update.Set(item => item.Experiment, assignment);

            var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        public async Task<long> CountInGroupAsync(string experimentId, string groupId, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq("experiment.experimentId", experimentId),
                Builders<UserDocument>.Filter.Eq("experiment.groupId", groupId),
                Builders<UserDocument>.Filter.Eq(item => item.Qa, null));

            return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
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

        public async Task<UserDocument?> FindByQaAliasAsync(string alias, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<UserDocument>.Filter.Eq("qa.alias", alias)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<UserDocument>> ListQaAsync(int limit, CancellationToken cancellationToken)
        {
            var projection = Builders<UserDocument>.Projection
                .Include(item => item.Qa)
                .Include(item => item.CreatedAt)
                .Include(item => item.UpdatedAt);

            return await Collection
                .Find(Builders<UserDocument>.Filter.Ne(item => item.Qa, null))
                .Project<UserDocument>(projection)
                .SortBy(item => item.Qa!.Alias)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> MarkQaAsync(string userId, UserQaDocument qa, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<UserDocument>.Update
                .Set(item => item.Qa, qa)
                .Set(item => item.Experiment, null)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(Builders<UserDocument>.Filter.Eq(item => item.Id, userId), update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        public async Task<bool> UnmarkQaAsync(string userId, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<UserDocument>.Update
                .Unset(item => item.Qa)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(Builders<UserDocument>.Filter.Eq(item => item.Id, userId), update, cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
                return false;

            var forcedFilter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq(item => item.Id, userId),
                Builders<UserDocument>.Filter.Eq("experiment.forced", true));

            await Collection.UpdateOneAsync(forcedFilter, Builders<UserDocument>.Update.Set(item => item.Experiment, null), cancellationToken: cancellationToken);

            return true;
        }

        public async Task<bool> SetQaExperimentAsync(string userId, UserExperimentDocument? assignment, DateTime now, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq(item => item.Id, userId),
                Builders<UserDocument>.Filter.Ne(item => item.Qa, null));
            var update = Builders<UserDocument>.Update
                .Set(item => item.Experiment, assignment)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
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

        public async Task<bool> TryReplaceRefreshAsync(string userId, string expectedRefreshTokenHash, UserDeviceDocument device, string country, DateTime now, CancellationToken cancellationToken)
        {
            return await _mongoTransactionRunner.ExecuteAsync(
                (session, token) => ReplaceRefreshAsync(session, userId, expectedRefreshTokenHash, device, country, now, token),
                cancellationToken);
        }

        public async Task<bool> UpsertDeviceAsync(string userId, UserDeviceDocument device, string country, DateTime now, CancellationToken cancellationToken)
        {
            return await _mongoTransactionRunner.ExecuteAsync(
                (session, token) => UpsertDeviceCoreAsync(session, userId, device, country, now, token),
                cancellationToken);
        }

        private async Task<bool> ReplaceRefreshAsync(IClientSessionHandle session, string userId, string expectedRefreshTokenHash, UserDeviceDocument device, string country, DateTime now, CancellationToken cancellationToken)
        {
            var user = await LoadUserAsync(session, userId, cancellationToken);

            if (user == null || user.Devices == null)
                return false;

            var devices = user.Devices;
            var index = FindRefreshIndex(devices, expectedRefreshTokenHash);

            if (index < 0)
                return false;

            var stored = CopyDevice(device);

            stored.PreviousRefreshTokenHash = expectedRefreshTokenHash;
            devices[index] = stored;

            return await SaveDevicesAsync(session, userId, devices, country, now, cancellationToken);
        }

        private async Task<bool> UpsertDeviceCoreAsync(IClientSessionHandle session, string userId, UserDeviceDocument device, string country, DateTime now, CancellationToken cancellationToken)
        {
            var user = await LoadUserAsync(session, userId, cancellationToken);

            if (user == null)
                return false;

            var devices = user.Devices;

            if (devices == null)
                devices = new List<UserDeviceDocument>();

            var stored = CopyDevice(device);
            var replaced = false;

            for (int i = 0; i < devices.Count; i++)
            {
                var existing = devices[i];

                if (string.Equals(existing.DeviceIdHash, stored.DeviceIdHash, StringComparison.Ordinal) == false)
                    continue;

                stored.PreviousRefreshTokenHash = existing.RefreshTokenHash;
                devices[i] = stored;
                replaced = true;

                break;
            }

            if (replaced == false)
                devices.Add(stored);

            return await SaveDevicesAsync(session, userId, devices, country, now, cancellationToken);
        }

        private async Task<UserDocument?> LoadUserAsync(IClientSessionHandle session, string userId, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.Eq(item => item.Id, userId);

            return await Collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<bool> SaveDevicesAsync(IClientSessionHandle session, string userId, List<UserDeviceDocument> devices, string country, DateTime now, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.Eq(item => item.Id, userId);
            var update = Builders<UserDocument>.Update
                .Set(item => item.Devices, devices)
                .Set(item => item.LastCountry, country)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        private UserDeviceDocument CopyDevice(UserDeviceDocument device)
        {
            return new UserDeviceDocument
            {
                DeviceIdHash = device.DeviceIdHash,
                RefreshTokenHash = device.RefreshTokenHash,
                PreviousRefreshTokenHash = device.PreviousRefreshTokenHash,
                RefreshExpiresAt = device.RefreshExpiresAt,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                ClientVersion = device.ClientVersion,
            };
        }

        private int FindRefreshIndex(List<UserDeviceDocument> devices, string refreshTokenHash)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                var existing = devices[i];

                if (string.Equals(existing.RefreshTokenHash, refreshTokenHash, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private FilterDefinition<UserDocument> CreateExperimentFilter(UserExperimentDocument? expected)
        {
            if (expected == null)
                return Builders<UserDocument>.Filter.Eq(item => item.Experiment, null);

            return Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq("experiment.experimentId", expected.ExperimentId),
                Builders<UserDocument>.Filter.Eq("experiment.groupId", expected.GroupId));
        }
    }
}
