using Microsoft.Extensions.Options;
using Server.Admin.Options;

namespace Server.Admin.Accounts
{
    internal sealed class AdminLoginThrottle
    {
        private readonly AdminPanelOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly Dictionary<string, AdminLoginAttempts> _attempts = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        public AdminLoginThrottle(IOptions<AdminPanelOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public bool IsLocked(string login)
        {
            lock (_sync)
            {
                if (_attempts.TryGetValue(login, out var attempts) == false)
                    return false;

                return _timeProvider.GetUtcNow() < attempts.LockedUntil;
            }
        }

        public void RegisterFailure(string login)
        {
            lock (_sync)
            {
                if (_attempts.TryGetValue(login, out var attempts) == false)
                {
                    attempts = new AdminLoginAttempts();
                    _attempts[login] = attempts;
                }

                attempts.Failures += 1;

                if (attempts.Failures < _options.MaxFailedLogins)
                    return;

                attempts.Failures = 0;
                attempts.LockedUntil = _timeProvider.GetUtcNow().AddMinutes(_options.LockoutMinutes);
            }
        }

        public void RegisterSuccess(string login)
        {
            lock (_sync)
            {
                _attempts.Remove(login);
            }
        }
    }
}
