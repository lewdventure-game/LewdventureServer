using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoTransactionRunner : IMongoTransactionRunner
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public MongoTransactionRunner(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        public async Task<TResult> ExecuteAsync<TResult>(Func<IClientSessionHandle, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
        {
            using var session = await _mongoDatabaseAccessor.Client.StartSessionAsync(cancellationToken: cancellationToken);

            var transactionOptions = new TransactionOptions(
                readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority);

            return await session.WithTransactionAsync(action, transactionOptions, cancellationToken);
        }
    }
}
