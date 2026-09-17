using MongoDB.Bson;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoTopologyValidator
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public MongoTopologyValidator(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        public async Task<string> GetReplicaSetNameAsync(CancellationToken cancellationToken)
        {
            var admin = _mongoDatabaseAccessor.Client.GetDatabase("admin");
            var hello = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);

            if (hello.TryGetValue("setName", out var setName))
                return setName.AsString;

            return string.Empty;
        }
    }
}
