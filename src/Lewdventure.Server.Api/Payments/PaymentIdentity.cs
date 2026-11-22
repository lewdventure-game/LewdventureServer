using MongoDB.Driver;
using Server.Infrastructure.Mongo.Players;

namespace Server.Api.Payments
{
    internal sealed class PaymentIdentity
    {
        public const string ScopeClaim = "payment_scope";

        private readonly UserRepository _users;
        private readonly PaymentStore _store;
        private readonly TimeProvider _timeProvider;

        public PaymentIdentity(UserRepository users, PaymentStore store, TimeProvider timeProvider)
        {
            _users = users;
            _store = store;
            _timeProvider = timeProvider;
        }

        public async Task<UserDocument> FindAsync(string scope, string subject, CancellationToken cancellationToken)
        {
            return await _users.FindByIdentityAsync(scope, subject, cancellationToken) ?? new UserDocument();
        }

        public async Task<UserDocument> GetAsync(string userId, CancellationToken cancellationToken)
        {
            return await _users.GetAsync(userId, cancellationToken) ?? new UserDocument();
        }

        public async Task<UserDocument> SignInAsync(string scope, string subject, CancellationToken cancellationToken)
        {
            var account = await _store.GetAccountAsync(scope, subject, cancellationToken);

            if (account != null)
            {
                var restored = await GetAsync(account.UserId, cancellationToken);

                if (restored.Id.Length != 0)
                    return ReadSubject(restored, scope) == subject ? restored : new UserDocument();

                return await InsertUserAsync(account.UserId, scope, subject, cancellationToken);
            }

            var existing = await FindAsync(scope, subject, cancellationToken);

            if (existing.Id.Length != 0)
                return await SaveAccountAsync(existing, scope, subject, cancellationToken);

            var user = await InsertUserAsync("usr_" + Guid.NewGuid().ToString("N"), scope, subject, cancellationToken);

            if (user.Id.Length == 0)
                return user;

            return await SaveAccountAsync(user, scope, subject, cancellationToken);
        }

        public string ReadSubject(UserDocument user, string scope)
        {
            foreach (var identity in user.Identities)
            {
                if (identity.Provider == scope)
                    return identity.Subject;
            }

            return string.Empty;
        }

        private async Task<UserDocument> InsertUserAsync(string userId, string scope, string subject, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var user = new UserDocument
            {
                Id = userId,
                CreatedAt = now,
                UpdatedAt = now,
                Identities = [new UserIdentityDocument { Provider = scope, Subject = subject, LinkedAt = now }],
            };

            try
            {
                await _users.InsertAsync(user, cancellationToken);

                return user;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                var existing = await FindAsync(scope, subject, cancellationToken);

                return existing.Id.Length == 0 ? await GetAsync(userId, cancellationToken) : existing;
            }
        }

        private async Task<UserDocument> SaveAccountAsync(UserDocument user, string scope, string subject, CancellationToken cancellationToken)
        {
            var account = await _store.SaveAccountAsync(scope, subject, user.Id, cancellationToken);

            return account == null || account.UserId != user.Id ? new UserDocument() : user;
        }
    }
}
