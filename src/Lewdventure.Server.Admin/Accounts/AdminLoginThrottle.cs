using Microsoft.Extensions.Options;
using Server.Admin.Options;

namespace Server.Admin.Accounts
{
    internal sealed class AdminLoginThrottle
    {
        private const int MaxTrackedLogins = 1000;

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
                var now = _timeProvider.GetUtcNow();

                if (_attempts.TryGetValue(login, out var attempts) == false)
                {
                    if (MaxTrackedLogins <= _attempts.Count)
                        RemoveStale(now);

                    attempts = new AdminLoginAttempts();
                    _attempts[login] = attempts;
                }

                attempts.Failures += 1;
                attempts.LastFailureAt = now;

                if (attempts.Failures < _options.MaxFailedLogins)
                    return;

                attempts.Failures = 0;
                attempts.LockedUntil = now.AddMinutes(_options.LockoutMinutes);
            }
        }

        public void RegisterSuccess(string login)
        {
            lock (_sync)
            {
                _attempts.Remove(login);
            }
        }

        private void RemoveStale(DateTimeOffset now)
        {
            var staleBefore = now.AddMinutes(-_options.LockoutMinutes);
            var stale = new List<string>();

            foreach (var pair in _attempts)
            {
                if (pair.Value.LockedUntil <= now && pair.Value.LastFailureAt < staleBefore)
                    stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                _attempts.Remove(stale[i]);

            if (_attempts.Count < MaxTrackedLogins)
                return;

            stale.Clear();

            foreach (var pair in _attempts)
            {
                if (pair.Value.LockedUntil <= now)
                    stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                _attempts.Remove(stale[i]);
        }
    }
}
