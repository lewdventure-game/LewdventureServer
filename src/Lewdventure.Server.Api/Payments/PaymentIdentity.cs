using MongoDB.Driver;
using Server.Infrastructure.Mongo.Players;

namespace Server.Api.Payments
{
    internal sealed class PaymentIdentity
    {
        public const string ScopeClaim = "payment_scope";

        private readonly UserRepository _users;
        private readonly TimeProvider _timeProvider;

        public PaymentIdentity(UserRepository users, TimeProvider timeProvider)
        {
            _users = users;
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
            var existing = await FindAsync(scope, subject, cancellationToken);

            if (existing.Id.Length != 0)
                return existing;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var user = new UserDocument
            {
                Id = "usr_" + Guid.NewGuid().ToString("N"),
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
                return await FindAsync(scope, subject, cancellationToken);
            }
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
    }
}
