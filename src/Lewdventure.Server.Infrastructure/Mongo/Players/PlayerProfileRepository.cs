using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerProfileRepository
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public PlayerProfileRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<PlayerProfileDocument> Collection => _mongoDatabaseAccessor.GetCollection<PlayerProfileDocument>(MongoCollectionNames.PlayerProfiles);

        public async Task<PlayerProfileDocument?> GetAsync(string userId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<PlayerProfileDocument>.Filter.Eq(item => item.Id, userId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<PlayerProfileDocument>> ListByStoryProgressAsync(int completedLevelId, int nextLevelId, int limit, CancellationToken cancellationToken)
        {
            var filters = new List<FilterDefinition<PlayerProfileDocument>>
            {
                Builders<PlayerProfileDocument>.Filter.AnyEq(item => item.Story.CompletedLevelIds, completedLevelId),
            };

            if (0 < nextLevelId)
                filters.Add(Builders<PlayerProfileDocument>.Filter.Not(Builders<PlayerProfileDocument>.Filter.AnyEq(item => item.Story.CompletedLevelIds, nextLevelId)));

            return await Collection
                .Find(Builders<PlayerProfileDocument>.Filter.And(filters))
                .SortByDescending(item => item.UpdatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> InsertIfMissingAsync(PlayerProfileDocument document, CancellationToken cancellationToken)
        {
            try
            {
                await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);

                return true;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<long> DeleteAsync(string userId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteOneAsync(Builders<PlayerProfileDocument>.Filter.Eq(item => item.Id, userId), cancellationToken);

            return result.DeletedCount;
        }

        public async Task<bool> ReplaceAsync(PlayerProfileDocument document, long expectedRev, CancellationToken cancellationToken)
        {
            var filter = Builders<PlayerProfileDocument>.Filter.And(
                Builders<PlayerProfileDocument>.Filter.Eq(item => item.Id, document.Id),
                Builders<PlayerProfileDocument>.Filter.Eq(item => item.Rev, expectedRev));
            var result = await Collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }
    }
}
