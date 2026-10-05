using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Qa
{
    internal sealed class QaTemplateRepository
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public QaTemplateRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<QaTemplateDocument> Collection => _mongoDatabaseAccessor.GetCollection<QaTemplateDocument>(MongoCollectionNames.QaTemplates);

        public async Task<QaTemplateDocument?> GetAsync(string templateId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<QaTemplateDocument>.Filter.Eq(item => item.Id, templateId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<QaTemplateDocument>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<QaTemplateDocument>.Filter.Empty)
                .SortBy(item => item.Name)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task UpsertAsync(QaTemplateDocument document, CancellationToken cancellationToken)
        {
            var filter = Builders<QaTemplateDocument>.Filter.Eq(item => item.Id, document.Id);

            await Collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }

        public async Task<long> DeleteAsync(string templateId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteOneAsync(Builders<QaTemplateDocument>.Filter.Eq(item => item.Id, templateId), cancellationToken);

            return result.DeletedCount;
        }
    }
}
