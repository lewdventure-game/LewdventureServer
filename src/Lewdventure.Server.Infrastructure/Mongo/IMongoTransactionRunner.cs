using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal interface IMongoTransactionRunner
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<IClientSessionHandle, CancellationToken, Task<TResult>> action, CancellationToken cancellationToken);
    }
}
