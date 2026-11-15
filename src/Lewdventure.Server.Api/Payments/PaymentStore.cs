using MongoDB.Bson;
using MongoDB.Driver;
using Server.Infrastructure.Mongo;

namespace Server.Api.Payments
{
    internal sealed class PaymentStore : IMongoIndexContributor
    {
        private const string OrdersName = "payment_orders";
        private const string EntitlementsName = "payment_entitlements";
        private const string MovementsName = "payment_movements";
        private const string LoginCodesName = "payment_login_codes";
        private const string ReportCursorsName = "payment_report_cursors";

        private readonly IMongoDatabaseAccessor _database;
        private readonly IMongoTransactionRunner _transactions;
        private readonly TimeProvider _timeProvider;

        public PaymentStore(IMongoDatabaseAccessor database, IMongoTransactionRunner transactions, TimeProvider timeProvider)
        {
            _database = database;
            _transactions = transactions;
            _timeProvider = timeProvider;
        }

        private IMongoCollection<PaymentOrder> Orders => _database.GetCollection<PaymentOrder>(OrdersName).WithWriteConcern(WriteConcern.WMajority);
        private IMongoCollection<PaymentEntitlement> Entitlements => _database.GetCollection<PaymentEntitlement>(EntitlementsName).WithReadConcern(ReadConcern.Majority);
        private IMongoCollection<PaymentMovement> Movements => _database.GetCollection<PaymentMovement>(MovementsName);
        private IMongoCollection<NutakuLoginCode> LoginCodes => _database.GetCollection<NutakuLoginCode>(LoginCodesName).WithWriteConcern(WriteConcern.WMajority);
        private IMongoCollection<PaymentReportCursor> Cursors => _database.GetCollection<PaymentReportCursor>(ReportCursorsName).WithWriteConcern(WriteConcern.WMajority);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys;

            return
            [
                new MongoIndexDefinition(OrdersName, new CreateIndexModel<BsonDocument>(keys.Ascending("scope").Ascending("nextCheckAt").Ascending("state"))),
                new MongoIndexDefinition(OrdersName, new CreateIndexModel<BsonDocument>(keys.Ascending("scope").Ascending("userId").Descending("createdAt"))),
                new MongoIndexDefinition(EntitlementsName, new CreateIndexModel<BsonDocument>(keys.Ascending("scope").Ascending("userId"))),
                new MongoIndexDefinition(LoginCodesName, new CreateIndexModel<BsonDocument>(keys.Ascending("expiresAt"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })),
            ];
        }

        public string OrderId(string scope, string externalId)
        {
            return scope + ":" + externalId;
        }

        public Task<PaymentOrder> GetAsync(string scope, string externalId, CancellationToken cancellationToken)
        {
            var id = OrderId(scope, externalId);

            return Orders.WithReadConcern(ReadConcern.Majority).Find(order => order.Id == id).FirstOrDefaultAsync(cancellationToken);
        }

        public Task<List<PaymentOrder>> ListRecentAsync(string scope, string userId, string subject, CancellationToken cancellationToken)
        {
            return Orders.WithReadConcern(ReadConcern.Majority)
                .Find(item => item.Scope == scope && item.UserId == userId && item.Subject == subject)
                .SortByDescending(item => item.CreatedAt)
                .Limit(50)
                .ToListAsync(cancellationToken);
        }

        public async Task<PaymentOrder> SaveNewAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            try
            {
                await Orders.InsertOneAsync(order, cancellationToken: cancellationToken);

                return order;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return await GetAsync(order.Scope, order.ExternalId, cancellationToken);
            }
        }

        public async Task<bool> BeginSteamAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.State, PaymentOrderState.Initiating)
                .Set(item => item.NextCheckAt, now.AddSeconds(30));
            var result = await Orders.UpdateOneAsync(item => item.Id == order.Id && item.State == PaymentOrderState.Created, update, cancellationToken: cancellationToken);

            return result.ModifiedCount == 1;
        }

        public Task SetSteamInitializedAsync(PaymentOrder order, string transactionId, CancellationToken cancellationToken)
        {
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.State, PaymentOrderState.AwaitingApproval)
                .Set(item => item.TransactionId, transactionId)
                .Set(item => item.NextCheckAt, _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(3));

            return Orders.UpdateOneAsync(item => item.Id == order.Id && item.State == PaymentOrderState.Initiating, update, cancellationToken: cancellationToken);
        }

        public Task<PaymentOrder> LeaseNextSteamOrderAsync(string scope, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var cutoff = now.AddDays(-1);
            var filter = Builders<PaymentOrder>.Filter.Where(order => order.Scope == scope && order.NextCheckAt <= now && cutoff <= order.CreatedAt &&
                (order.State == PaymentOrderState.Initiating || order.State == PaymentOrderState.AwaitingApproval));
            var update = Builders<PaymentOrder>.Update.Set(order => order.NextCheckAt, now.AddSeconds(45));
            var options = new FindOneAndUpdateOptions<PaymentOrder>
            {
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<PaymentOrder>.Sort.Ascending(order => order.NextCheckAt),
            };

            return Orders.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        }

        public Task FailSteamOrderAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            var filter = Builders<PaymentOrder>.Filter.Where(item => item.Id == order.Id &&
                (item.State == PaymentOrderState.Initiating || item.State == PaymentOrderState.AwaitingApproval));
            var update = Builders<PaymentOrder>.Update.Set(item => item.State, PaymentOrderState.Failed);

            return Orders.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        }

        public Task<bool> SettleAsync(PaymentOrder expected, bool reverse, string transactionId, string evidence, CancellationToken cancellationToken)
        {
            return _transactions.ExecuteAsync((session, token) => SettleAsync(session, expected, reverse, transactionId, evidence, token), cancellationToken);
        }

        public Task<List<PaymentEntitlement>> GetEntitlementsAsync(string scope, string userId, CancellationToken cancellationToken)
        {
            return Entitlements.Find(item => item.Scope == scope && item.UserId == userId).ToListAsync(cancellationToken);
        }

        public Task<List<PaymentMovement>> GetMovementsAsync(string orderId, CancellationToken cancellationToken)
        {
            var ids = new[] { orderId + ":granted", orderId + ":reversed" };
            var filter = Builders<PaymentMovement>.Filter.In(item => item.Id, ids);

            return Movements.WithReadConcern(ReadConcern.Majority).Find(filter).SortBy(item => item.RecordedAt).ToListAsync(cancellationToken);
        }

        public Task AddLoginCodeAsync(NutakuLoginCode code, CancellationToken cancellationToken)
        {
            return LoginCodes.InsertOneAsync(code, cancellationToken: cancellationToken);
        }

        public Task<NutakuLoginCode> ConsumeLoginCodeAsync(string codeHash, string scope, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return LoginCodes.FindOneAndDeleteAsync(code => code.Id == codeHash && code.Scope == scope && now < code.ExpiresAt, cancellationToken: cancellationToken);
        }

        public async Task<DateTime> GetReportCursorAsync(string scope, CancellationToken cancellationToken)
        {
            var initialTime = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
            var update = Builders<PaymentReportCursor>.Update.SetOnInsert(item => item.Time, initialTime);
            var options = new FindOneAndUpdateOptions<PaymentReportCursor> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            var cursor = await Cursors.FindOneAndUpdateAsync(item => item.Id == scope, update, options, cancellationToken);

            return cursor.Time;
        }

        public Task AdvanceReportCursorAsync(string scope, DateTime time, CancellationToken cancellationToken)
        {
            return Cursors.UpdateOneAsync(item => item.Id == scope, Builders<PaymentReportCursor>.Update.Max(item => item.Time, time),
                new UpdateOptions { IsUpsert = true }, cancellationToken);
        }

        private async Task<bool> SettleAsync(IClientSessionHandle session, PaymentOrder expected, bool reverse, string transactionId, string evidence, CancellationToken cancellationToken)
        {
            var order = await Orders.Find(session, item => item.Id == expected.Id).FirstOrDefaultAsync(cancellationToken);

            if (order == null || order.UserId != expected.UserId || order.Subject != expected.Subject ||
                (order.TransactionId.Length != 0 && order.TransactionId != transactionId))
                return false;

            var target = reverse ? PaymentOrderState.Reversed : PaymentOrderState.Granted;

            if (order.State == target)
                return true;

            if (order.State == PaymentOrderState.Reversed)
                return false;

            var update = Builders<PaymentOrder>.Update.Set(item => item.State, target).Set(item => item.TransactionId, transactionId);
            await Orders.UpdateOneAsync(session, item => item.Id == order.Id, update, cancellationToken: cancellationToken);

            if (reverse)
                await Entitlements.DeleteOneAsync(session, item => item.Id == order.Id, cancellationToken: cancellationToken);
            else
                await Entitlements.InsertOneAsync(session, new PaymentEntitlement
                {
                    Id = order.Id,
                    UserId = order.UserId,
                    Scope = order.Scope,
                    Entitlement = order.Entitlement,
                }, cancellationToken: cancellationToken);

            var action = reverse ? "reversed" : "granted";
            await Movements.InsertOneAsync(session, new PaymentMovement
            {
                Id = order.Id + ":" + action,
                OrderId = order.Id,
                UserId = order.UserId,
                Scope = order.Scope,
                Entitlement = order.Entitlement,
                Action = action,
                Evidence = evidence,
                RecordedAt = _timeProvider.GetUtcNow().UtcDateTime,
            }, cancellationToken: cancellationToken);

            return true;
        }
    }
}
