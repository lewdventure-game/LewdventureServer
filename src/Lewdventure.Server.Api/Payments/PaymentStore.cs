using MongoDB.Bson;
using MongoDB.Driver;
using Server.Battles;
using Server.Infrastructure.Alerts;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Api.Payments
{
    internal sealed class PaymentStore : IMongoIndexContributor
    {
        private const string OrdersName = "payment_orders";
        private const string EntitlementsName = "payment_entitlements";
        private const string MovementsName = "payment_movements";
        private const string LoginCodesName = "payment_login_codes";
        private const string ReportCursorsName = "payment_report_cursors";
        private const string AccountsName = "payment_accounts";
        private const string AuditsName = "payment_audits";
        private const int Rejected = 0;
        private const int Committed = 1;
        private const int ProfileConflict = 2;
        private const int CommittedWithReview = 3;
        private const int MaxAttempts = 3;

        private readonly IMongoDatabaseAccessor _database;
        private readonly IMongoTransactionRunner _transactions;
        private readonly TimeProvider _timeProvider;
        private readonly PlayerProfileRepository _profiles;
        private readonly PlayerProfileService _profileService;
        private readonly PlayerLedgerRepository _ledger;
        private readonly RewardApplier _rewardApplier;
        private readonly PaymentRewardPolicy _rewards;
        private readonly PaymentMetrics _metrics;
        private readonly IAlertPublisher _alerts;

        public PaymentStore(
            IMongoDatabaseAccessor database,
            IMongoTransactionRunner transactions,
            TimeProvider timeProvider,
            PlayerProfileRepository profiles,
            PlayerProfileService profileService,
            PlayerLedgerRepository ledger,
            RewardApplier rewardApplier,
            PaymentRewardPolicy rewards,
            PaymentMetrics metrics,
            IAlertPublisher alerts)
        {
            _database = database;
            _transactions = transactions;
            _timeProvider = timeProvider;
            _profiles = profiles;
            _profileService = profileService;
            _ledger = ledger;
            _rewardApplier = rewardApplier;
            _rewards = rewards;
            _metrics = metrics;
            _alerts = alerts;
        }

        private IMongoCollection<PaymentOrder> Orders => _database.GetCollection<PaymentOrder>(OrdersName).WithWriteConcern(WriteConcern.WMajority);
        private IMongoCollection<PaymentEntitlement> Entitlements => _database.GetCollection<PaymentEntitlement>(EntitlementsName).WithReadConcern(ReadConcern.Majority);
        private IMongoCollection<PaymentMovement> Movements => _database.GetCollection<PaymentMovement>(MovementsName);
        private IMongoCollection<NutakuLoginCode> LoginCodes => _database.GetCollection<NutakuLoginCode>(LoginCodesName).WithWriteConcern(WriteConcern.WMajority);
        private IMongoCollection<PaymentReportCursor> Cursors => _database.GetCollection<PaymentReportCursor>(ReportCursorsName).WithWriteConcern(WriteConcern.WMajority);
        private IMongoCollection<PaymentAccount> Accounts => _database.GetCollection<PaymentAccount>(AccountsName).WithWriteConcern(WriteConcern.WMajority);
        private IMongoCollection<PaymentAuditEvent> Audits => _database.GetCollection<PaymentAuditEvent>(AuditsName).WithWriteConcern(WriteConcern.WMajority);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys;

            return
            [
                new MongoIndexDefinition(OrdersName, new CreateIndexModel<BsonDocument>(keys.Ascending("scope").Ascending("nextCheckAt").Ascending("state"))),
                new MongoIndexDefinition(OrdersName, new CreateIndexModel<BsonDocument>(keys.Ascending("scope").Ascending("userId").Descending("createdAt"))),
                new MongoIndexDefinition(OrdersName, new CreateIndexModel<BsonDocument>(keys.Ascending("userId").Descending("createdAt"))),
                new MongoIndexDefinition(OrdersName, new CreateIndexModel<BsonDocument>(keys.Ascending("reviewRequired").Ascending("updatedAt"))),
                new MongoIndexDefinition(EntitlementsName, new CreateIndexModel<BsonDocument>(keys.Ascending("scope").Ascending("userId"))),
                new MongoIndexDefinition(EntitlementsName, new CreateIndexModel<BsonDocument>(keys.Ascending("userId"))),
                new MongoIndexDefinition(LoginCodesName, new CreateIndexModel<BsonDocument>(keys.Ascending("expiresAt"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })),
                new MongoIndexDefinition(AccountsName, new CreateIndexModel<BsonDocument>(keys.Ascending("userId"))),
                new MongoIndexDefinition(AuditsName, new CreateIndexModel<BsonDocument>(keys.Ascending("orderId").Descending("createdAt"))),
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

        public Task<List<PaymentOrder>> ListForUserAsync(string userId, CancellationToken cancellationToken)
        {
            return Orders.WithReadConcern(ReadConcern.Majority)
                .Find(item => item.UserId == userId)
                .SortByDescending(item => item.CreatedAt)
                .Limit(200)
                .ToListAsync(cancellationToken);
        }

        public Task<List<PaymentOrder>> ListReviewAsync(CancellationToken cancellationToken)
        {
            return Orders.WithReadConcern(ReadConcern.Majority)
                .Find(item => item.ReviewRequired)
                .SortBy(item => item.UpdatedAt)
                .Limit(100)
                .ToListAsync(cancellationToken);
        }

        public Task<long> CountReviewAsync(CancellationToken cancellationToken)
        {
            return Orders.CountDocumentsAsync(item => item.ReviewRequired, cancellationToken: cancellationToken);
        }

        public async Task<PaymentOrder> SaveNewAsync(PaymentOrder order, CancellationToken cancellationToken)
        {
            if (order.UpdatedAt == DateTime.MinValue)
                order.UpdatedAt = order.CreatedAt;

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
                .Set(item => item.NextCheckAt, now.AddSeconds(30))
                .Set(item => item.UpdatedAt, now)
                .Set(item => item.InitializationAttempts, 1)
                .Set(item => item.LastError, string.Empty);
            var result = await Orders.UpdateOneAsync(item => item.Id == order.Id && item.State == PaymentOrderState.Created, update, cancellationToken: cancellationToken);

            return result.ModifiedCount == 1;
        }

        public Task SetSteamInitializedAsync(PaymentOrder order, string transactionId, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.State, PaymentOrderState.AwaitingApproval)
                .Set(item => item.TransactionId, transactionId)
                .Set(item => item.NextCheckAt, now.AddSeconds(3))
                .Set(item => item.UpdatedAt, now)
                .Set(item => item.LastError, string.Empty);

            return Orders.UpdateOneAsync(item => item.Id == order.Id && item.State == PaymentOrderState.Initiating, update, cancellationToken: cancellationToken);
        }

        public Task RecordSteamInitializationErrorAsync(PaymentOrder order, string error, bool incrementAttempt, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.LastError, error)
                .Set(item => item.NextCheckAt, now.AddSeconds(45));

            if (incrementAttempt)
                update = update.Inc(item => item.InitializationAttempts, 1);

            return Orders.UpdateOneAsync(item => item.Id == order.Id && item.State == PaymentOrderState.Initiating, update, cancellationToken: cancellationToken);
        }

        public Task RecordSteamQueryErrorAsync(PaymentOrder order, string error, CancellationToken cancellationToken)
        {
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.LastError, error)
                .Set(item => item.NextCheckAt, _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(45));

            return Orders.UpdateOneAsync(item => item.Id == order.Id, update, cancellationToken: cancellationToken);
        }

        public Task<PaymentOrder> LeaseNextSteamOrderAsync(string scope, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var cutoff = now.AddDays(-1);
            var filter = Builders<PaymentOrder>.Filter;
            var recent = filter.Or(
                filter.And(filter.Ne(item => item.UpdatedAt, DateTime.MinValue), filter.Gte(item => item.UpdatedAt, cutoff)),
                filter.And(filter.Eq(item => item.UpdatedAt, DateTime.MinValue), filter.Gte(item => item.CreatedAt, cutoff)));
            var pending = filter.Or(
                filter.Eq(item => item.State, PaymentOrderState.Initiating),
                filter.Eq(item => item.State, PaymentOrderState.AwaitingApproval));
            var query = filter.And(
                filter.Eq(item => item.Scope, scope),
                filter.Lte(item => item.NextCheckAt, now),
                filter.Ne(item => item.ReviewRequired, true),
                pending,
                recent);
            var update = Builders<PaymentOrder>.Update.Set(order => order.NextCheckAt, now.AddSeconds(45));
            var options = new FindOneAndUpdateOptions<PaymentOrder>
            {
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<PaymentOrder>.Sort.Ascending(order => order.NextCheckAt),
            };

            return Orders.FindOneAndUpdateAsync(query, update, options, cancellationToken);
        }

        public Task<List<PaymentOrder>> ListStaleSteamOrdersAsync(string scope, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var cutoff = now.AddDays(-1);
            var filter = Builders<PaymentOrder>.Filter;
            var stale = filter.Or(
                filter.And(filter.Ne(item => item.UpdatedAt, DateTime.MinValue), filter.Lt(item => item.UpdatedAt, cutoff)),
                filter.And(filter.Eq(item => item.UpdatedAt, DateTime.MinValue), filter.Lt(item => item.CreatedAt, cutoff)));
            var pending = filter.Or(
                filter.Eq(item => item.State, PaymentOrderState.Initiating),
                filter.Eq(item => item.State, PaymentOrderState.AwaitingApproval));

            return Orders.Find(filter.And(
                    filter.Eq(item => item.Scope, scope),
                    filter.Ne(item => item.ReviewRequired, true),
                    pending,
                    stale))
                .SortBy(item => item.UpdatedAt)
                .Limit(50)
                .ToListAsync(cancellationToken);
        }

        public Task FailSteamOrderAsync(PaymentOrder order, string error, CancellationToken cancellationToken)
        {
            var filter = Builders<PaymentOrder>.Filter.Where(item => item.Id == order.Id &&
                (item.State == PaymentOrderState.Initiating || item.State == PaymentOrderState.AwaitingApproval));
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.State, PaymentOrderState.Failed)
                .Set(item => item.LastError, error)
                .Set(item => item.UpdatedAt, _timeProvider.GetUtcNow().UtcDateTime);

            return Orders.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        }

        public async Task<bool> SettleAsync(
            PaymentOrder expected,
            bool reverse,
            string transactionId,
            string evidence,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            if (reverse == false && expected.Rewards.Length != 0)
                await _profileService.GetOrCreateAsync(expected.UserId, cancellationToken);

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var result = await _transactions.ExecuteAsync(
                    (session, token) => SettleAsync(session, expected, reverse, transactionId, evidence, configDistributor, token),
                    cancellationToken);

                if (result == ProfileConflict)
                    continue;

                if (result == Rejected)
                    return false;

                if (result == CommittedWithReview)
                    PublishReview(expected, "reward_recovery_required");

                _metrics.Settlement(expected.Scope, reverse);

                return true;
            }

            throw new PaymentSettlementException(true);
        }

        public async Task RestoreGrantedRewardsAsync(string userId, IConfigDistributor configDistributor, CancellationToken cancellationToken)
        {
            var orders = await Orders.Find(item => item.UserId == userId && item.State == PaymentOrderState.Granted && item.Rewards.Length != 0)
                .ToListAsync(cancellationToken);

            if (orders.Count == 0)
                return;

            var ledgerIds = new List<string>(orders.Count);

            for (var i = 0; i < orders.Count; i++)
                ledgerIds.Add("payment:" + orders[i].Id + ":payment_grant");

            var existing = await _ledger.ListExistingIdsAsync(ledgerIds, cancellationToken);
            var missing = new List<PaymentOrder>();

            for (var i = 0; i < orders.Count; i++)
            {
                if (existing.Contains(ledgerIds[i]) == false)
                    missing.Add(orders[i]);
            }

            if (missing.Count == 0)
                return;

            await _profileService.GetOrCreateAsync(userId, cancellationToken);

            for (var i = 0; i < missing.Count; i++)
            {
                var order = missing[i];
                var restored = false;

                for (var attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    var result = await _transactions.ExecuteAsync(
                        (session, token) => RestoreRewardAsync(session, order, configDistributor, token),
                        cancellationToken);

                    if (result == ProfileConflict)
                        continue;

                    restored = true;

                    break;
                }

                if (restored == false)
                    throw new PaymentSettlementException(true);
            }
        }

        public Task<List<PaymentEntitlement>> GetEntitlementsAsync(string scope, string userId, CancellationToken cancellationToken)
        {
            return Entitlements.Find(item => item.Scope == scope && item.UserId == userId).ToListAsync(cancellationToken);
        }

        public Task<List<PaymentEntitlement>> GetEntitlementsForUserAsync(string userId, CancellationToken cancellationToken)
        {
            return Entitlements.Find(item => item.UserId == userId).Limit(500).ToListAsync(cancellationToken);
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

        public Task<PaymentAccount> GetAccountAsync(string scope, string subject, CancellationToken cancellationToken)
        {
            var id = OrderId(scope, subject);

            return Accounts.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PaymentAccount> SaveAccountAsync(string scope, string subject, string userId, CancellationToken cancellationToken)
        {
            var account = new PaymentAccount
            {
                Id = OrderId(scope, subject),
                Scope = scope,
                UserId = userId,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            };

            try
            {
                await Accounts.InsertOneAsync(account, cancellationToken: cancellationToken);

                return account;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return await Accounts.Find(item => item.Id == account.Id).FirstOrDefaultAsync(cancellationToken);
            }
        }

        public Task AddAuditAsync(PaymentOrder order, string action, string actor, string reference, string detail, CancellationToken cancellationToken)
        {
            return Audits.InsertOneAsync(CreateAudit(order, action, actor, reference, detail), cancellationToken: cancellationToken);
        }

        public Task<List<PaymentAuditEvent>> GetAuditsAsync(string orderId, CancellationToken cancellationToken)
        {
            return Audits.WithReadConcern(ReadConcern.Majority)
                .Find(item => item.OrderId == orderId)
                .SortByDescending(item => item.CreatedAt)
                .Limit(100)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> MarkReviewAsync(PaymentOrder expected, string reason, string evidence, CancellationToken cancellationToken)
        {
            var update = Builders<PaymentOrder>.Update
                .Set(item => item.ReviewRequired, true)
                .Set(item => item.ReviewReason, reason)
                .Set(item => item.LastError, evidence)
                .Set(item => item.UpdatedAt, _timeProvider.GetUtcNow().UtcDateTime);
            var result = await Orders.UpdateOneAsync(item => item.Id == expected.Id && item.ReviewRequired != true, update, cancellationToken: cancellationToken);

            if (result.ModifiedCount == 0)
                return false;

            await AddAuditAsync(expected, "review_opened", "server", string.Empty, reason + ":" + evidence, cancellationToken);
            PublishReview(expected, reason);
            _metrics.Review(expected.Scope, reason);

            return true;
        }

        public async Task<bool> ResolveReviewAsync(
            PaymentOrder expected,
            string resolution,
            string actor,
            string reference,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var result = await _transactions.ExecuteAsync(
                    (session, token) => ResolveReviewAsync(session, expected, resolution, actor, reference, token),
                    cancellationToken);

                if (result == ProfileConflict)
                    continue;

                if (result == Rejected)
                    return false;

                var alert = new AlertMessage(AlertSeverity.Resolved, "Payment review resolved", expected.Id, "payment-review:" + expected.Id);
                _alerts.Publish(alert);

                return true;
            }

            throw new PaymentSettlementException(true);
        }

        public async Task<DateTime> GetReportCursorAsync(string scope, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var initialTime = now.AddDays(-1);
            var update = Builders<PaymentReportCursor>.Update
                .SetOnInsert(item => item.Time, initialTime)
                .SetOnInsert(item => item.CreatedAt, now);
            var options = new FindOneAndUpdateOptions<PaymentReportCursor> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
            var cursor = await Cursors.FindOneAndUpdateAsync(item => item.Id == scope, update, options, cancellationToken);

            return cursor.Time;
        }

        public Task<PaymentReportCursor> GetReportStateAsync(string scope, CancellationToken cancellationToken)
        {
            return Cursors.Find(item => item.Id == scope).FirstOrDefaultAsync(cancellationToken);
        }

        public Task RecordReportAttemptAsync(string scope, CancellationToken cancellationToken)
        {
            var update = Builders<PaymentReportCursor>.Update.Set(item => item.LastAttemptAt, _timeProvider.GetUtcNow().UtcDateTime);

            return Cursors.UpdateOneAsync(item => item.Id == scope, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }

        public Task RecordReportSuccessAsync(string scope, DateTime time, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var update = Builders<PaymentReportCursor>.Update
                .Max(item => item.Time, time)
                .Set(item => item.LastSuccessAt, now)
                .Set(item => item.LastError, string.Empty)
                .SetOnInsert(item => item.CreatedAt, now);

            return Cursors.UpdateOneAsync(item => item.Id == scope, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }

        public Task RecordReportFailureAsync(string scope, string error, CancellationToken cancellationToken)
        {
            var update = Builders<PaymentReportCursor>.Update
                .Set(item => item.LastError, error)
                .Set(item => item.LastAttemptAt, _timeProvider.GetUtcNow().UtcDateTime)
                .SetOnInsert(item => item.CreatedAt, _timeProvider.GetUtcNow().UtcDateTime);

            return Cursors.UpdateOneAsync(item => item.Id == scope, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }

        private async Task<int> SettleAsync(
            IClientSessionHandle session,
            PaymentOrder expected,
            bool reverse,
            string transactionId,
            string evidence,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            var order = await Orders.Find(session, item => item.Id == expected.Id).FirstOrDefaultAsync(cancellationToken);

            if (order == null || order.UserId != expected.UserId || order.Subject != expected.Subject ||
                (order.TransactionId.Length != 0 && order.TransactionId != transactionId))
                return Rejected;

            var target = reverse ? PaymentOrderState.Reversed : PaymentOrderState.Granted;

            if (order.State == target)
                return Committed;

            if (order.State == PaymentOrderState.Reversed)
                return Rejected;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var reviewRequired = false;

            if (reverse == false && order.Rewards.Length != 0)
            {
                var result = await GrantRewardsAsync(session, order, evidence, configDistributor, now, cancellationToken);

                if (result != Committed)
                    return result;
            }

            if (reverse && order.State == PaymentOrderState.Granted && order.Rewards.Length != 0)
            {
                var result = await ReverseRewardsAsync(session, order, evidence, false, now, cancellationToken);

                if (result == ProfileConflict)
                    return result;

                reviewRequired = result == CommittedWithReview;
            }

            var update = Builders<PaymentOrder>.Update
                .Set(item => item.State, target)
                .Set(item => item.TransactionId, transactionId)
                .Set(item => item.UpdatedAt, now)
                .Set(item => item.LastError, string.Empty)
                .Set(item => item.ReviewRequired, reviewRequired)
                .Set(item => item.ReviewReason, reviewRequired ? "reward_recovery_required" : string.Empty);

            if (reverse)
                update = update.Set(item => item.ReversedAt, now);
            else
                update = update.Set(item => item.GrantedAt, now);

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
                RewardsRecovered = reverse && order.Rewards.Length != 0 && reviewRequired == false,
                RecordedAt = now,
            }, cancellationToken: cancellationToken);

            return reviewRequired ? CommittedWithReview : Committed;
        }

        private async Task<int> GrantRewardsAsync(
            IClientSessionHandle session,
            PaymentOrder order,
            string evidence,
            IConfigDistributor configDistributor,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var profile = await _profiles.GetAsync(session, order.UserId, cancellationToken);

            if (profile == null)
                throw new PaymentSettlementException(false);

            var expectedRev = profile.Rev;
            var rewards = _rewards.Parse(order.Rewards);

            if (CanGrant(profile, CreateRecoveryTotals(order.Rewards)) == false)
                throw new PaymentSettlementException(false);

            var entries = _rewardApplier.Apply(profile, rewards, configDistributor, now);

            if (rewards.Count == 0 || entries.Count != rewards.Count)
                throw new PaymentSettlementException(false);

            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            if (await _profiles.ReplaceAsync(session, profile, expectedRev, cancellationToken) == false)
                return ProfileConflict;

            await _ledger.AppendAsync(session, CreateLedger(order, "payment_grant", evidence, profile.Rev, now, entries), cancellationToken);

            return Committed;
        }

        private async Task<int> ReverseRewardsAsync(
            IClientSessionHandle session,
            PaymentOrder order,
            string evidence,
            bool availableOnly,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var profile = await _profiles.GetAsync(session, order.UserId, cancellationToken);
            var totals = CreateRecoveryTotals(order.Rewards);

            if (profile == null || (availableOnly == false && CanRecover(profile, totals) == false))
                return CommittedWithReview;

            var expectedRev = profile.Rev;
            var entries = ApplyRecovery(profile, totals, availableOnly);

            if (entries.Count == 0)
                return Committed;

            profile.Rev = expectedRev + 1;
            profile.UpdatedAt = now;

            if (await _profiles.ReplaceAsync(session, profile, expectedRev, cancellationToken) == false)
                return ProfileConflict;

            await _ledger.AppendAsync(session, CreateLedger(order, "payment_reverse", evidence, profile.Rev, now, entries), cancellationToken);

            return Committed;
        }

        private async Task<int> RestoreRewardAsync(
            IClientSessionHandle session,
            PaymentOrder order,
            IConfigDistributor configDistributor,
            CancellationToken cancellationToken)
        {
            var ledgerId = "payment:" + order.Id + ":payment_grant";

            if (await _ledger.ExistsAsync(session, ledgerId, cancellationToken))
                return Committed;

            var current = await Orders.Find(session, item => item.Id == order.Id).FirstOrDefaultAsync(cancellationToken);

            if (current == null || current.State != PaymentOrderState.Granted)
                return Committed;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return await GrantRewardsAsync(session, current, "payment:profile_restore", configDistributor, now, cancellationToken);
        }

        private async Task<int> ResolveReviewAsync(
            IClientSessionHandle session,
            PaymentOrder expected,
            string resolution,
            string actor,
            string reference,
            CancellationToken cancellationToken)
        {
            var order = await Orders.Find(session, item => item.Id == expected.Id).FirstOrDefaultAsync(cancellationToken);

            if (order == null)
                return Rejected;

            if (order.ReviewRequired == false)
                return Committed;

            var pending = order.State == PaymentOrderState.Initiating || order.State == PaymentOrderState.AwaitingApproval;

            if ((pending && resolution != "retry") || (resolution == "retry" && pending == false) ||
                (resolution == "deduct_available" && (order.State != PaymentOrderState.Reversed || order.Rewards.Length == 0)) ||
                (resolution == "rewards_kept" && order.State != PaymentOrderState.Reversed))
                return Rejected;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (resolution == "deduct_available")
            {
                var result = await ReverseRewardsAsync(session, order, actor + ":" + reference, true, now, cancellationToken);

                if (result == ProfileConflict)
                    return result;
            }

            var update = Builders<PaymentOrder>.Update
                .Set(item => item.ReviewRequired, false)
                .Set(item => item.ReviewReason, string.Empty)
                .Set(item => item.LastError, string.Empty)
                .Set(item => item.UpdatedAt, now)
                .Set(item => item.NextCheckAt, now);
            await Orders.UpdateOneAsync(session, item => item.Id == order.Id, update, cancellationToken: cancellationToken);
            await Audits.InsertOneAsync(session, CreateAudit(order, "review_resolved", actor, reference, resolution), cancellationToken: cancellationToken);

            return Committed;
        }

        private Dictionary<string, long> CreateRecoveryTotals(string rawRewards)
        {
            var rewards = _rewards.Parse(rawRewards);
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                var key = (int)reward.Type + ":" + reward.RewardKey;
                totals[key] = totals.GetValueOrDefault(key) + reward.Count;
            }

            return totals;
        }

        private bool CanGrant(PlayerProfileDocument profile, Dictionary<string, long> totals)
        {
            foreach (var pair in totals)
            {
                var key = pair.Key.Substring(pair.Key.IndexOf(':') + 1);

                if (pair.Key.StartsWith(((int)BattleRewardType.Resource).ToString() + ":", StringComparison.Ordinal))
                {
                    profile.Resources.TryGetValue(key, out var current);

                    if (long.MaxValue - pair.Value < current)
                        return false;
                }
                else
                {
                    profile.Flags.TryGetValue(key, out var current);

                    if (int.MaxValue - pair.Value < current)
                        return false;
                }
            }

            return true;
        }

        private bool CanRecover(PlayerProfileDocument profile, Dictionary<string, long> totals)
        {
            foreach (var pair in totals)
            {
                if (pair.Key.StartsWith(((int)BattleRewardType.Resource).ToString() + ":", StringComparison.Ordinal))
                {
                    var key = pair.Key.Substring(pair.Key.IndexOf(':') + 1);
                    profile.Resources.TryGetValue(key, out var current);

                    if (current < pair.Value)
                        return false;
                }
                else
                {
                    var key = pair.Key.Substring(pair.Key.IndexOf(':') + 1);
                    profile.Flags.TryGetValue(key, out var current);

                    if (current < pair.Value)
                        return false;
                }
            }

            return true;
        }

        private List<PlayerLedgerEntryDocument> ApplyRecovery(PlayerProfileDocument profile, Dictionary<string, long> totals, bool availableOnly)
        {
            var entries = new List<PlayerLedgerEntryDocument>();

            foreach (var pair in totals)
            {
                var resource = pair.Key.StartsWith(((int)BattleRewardType.Resource).ToString() + ":", StringComparison.Ordinal);
                var key = pair.Key.Substring(pair.Key.IndexOf(':') + 1);
                long current;

                if (resource)
                {
                    profile.Resources.TryGetValue(key, out current);
                    var amount = availableOnly ? Math.Min(current, pair.Value) : pair.Value;
                    profile.Resources[key] = current - amount;

                    if (0 < amount)
                        entries.Add(new PlayerLedgerEntryDocument { Type = "resource", Key = key, Amount = -amount });
                }
                else
                {
                    profile.Flags.TryGetValue(key, out var currentFlag);
                    current = currentFlag;
                    var amount = availableOnly ? Math.Min(current, pair.Value) : pair.Value;
                    profile.Flags[key] = (int)(current - amount);

                    if (0 < amount)
                        entries.Add(new PlayerLedgerEntryDocument { Type = "flag", Key = key, Amount = -amount });
                }
            }

            return entries;
        }

        private PlayerLedgerDocument CreateLedger(PaymentOrder order, string action, string source, long rev, DateTime now, List<PlayerLedgerEntryDocument> entries)
        {
            return new PlayerLedgerDocument
            {
                Id = "payment:" + order.Id + ":" + action,
                UserId = order.UserId,
                Action = action,
                Source = source,
                RequestId = order.Id,
                Rev = rev,
                CreatedAt = now,
                UpdatedAt = now,
                Entries = entries,
            };
        }

        private PaymentAuditEvent CreateAudit(PaymentOrder order, string action, string actor, string reference, string detail)
        {
            return new PaymentAuditEvent
            {
                Id = order.Id + ":" + Guid.NewGuid().ToString("N"),
                OrderId = order.Id,
                Scope = order.Scope,
                UserId = order.UserId,
                Action = action,
                Actor = actor,
                Reference = reference,
                Detail = detail,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            };
        }

        private void PublishReview(PaymentOrder order, string reason)
        {
            var alert = new AlertMessage(AlertSeverity.Critical, "Payment requires review", order.Id, "payment-review:" + order.Id);
            alert.Fields.Add(new KeyValuePair<string, string>("platform", order.Scope));
            alert.Fields.Add(new KeyValuePair<string, string>("reason", reason));
            _alerts.Publish(alert);
        }
    }
}
