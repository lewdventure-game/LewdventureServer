using Microsoft.Extensions.Options;
using Server.Infrastructure.Analytics;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerAuthService
    {
        private const string UserIdPrefix = "usr_";
        private const string BannedError = "Account is banned.";
        private const string UnknownSessionError = "Refresh token is not valid.";
        private const string ReusedTokenReason = "reused";
        private const string UnknownTokenReason = "unknown";

        private readonly ExperimentAssignmentService _experimentAssignmentService;
        private readonly ILogger<PlayerAuthService> _logger;
        private readonly AuthOptions _options;
        private readonly PlayerAnalytics _playerAnalytics;
        private readonly TimeProvider _timeProvider;
        private readonly TokenGenerator _tokenGenerator;
        private readonly UserRepository _userRepository;

        public PlayerAuthService(
            ExperimentAssignmentService experimentAssignmentService,
            ILogger<PlayerAuthService> logger,
            IOptions<AuthOptions> options,
            PlayerAnalytics playerAnalytics,
            TimeProvider timeProvider,
            TokenGenerator tokenGenerator,
            UserRepository userRepository)
        {
            _experimentAssignmentService = experimentAssignmentService;
            _logger = logger;
            _options = options.Value;
            _playerAnalytics = playerAnalytics;
            _timeProvider = timeProvider;
            _tokenGenerator = tokenGenerator;
            _userRepository = userRepository;
        }

        public async Task<AuthSessionResult> AuthenticateDeviceAsync(string deviceId, string clientVersion, string country, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var deviceIdHash = _tokenGenerator.Hash(deviceId);
            var user = await _userRepository.FindByDeviceAsync(deviceIdHash, cancellationToken);
            var isNewPlayer = user == null;

            if (user == null)
            {
                user = new UserDocument
                {
                    Id = UserIdPrefix + Guid.NewGuid().ToString("N"),
                    CreatedAt = now,
                    UpdatedAt = now,
                    Country = country,
                    LastCountry = country,
                };

                await _userRepository.InsertAsync(user, cancellationToken);
                _logger.LogInformation("[Auth] account created userId = {UserId}", user.Id);
                _playerAnalytics.TrackAccountCreated(user.Id, country, clientVersion);
            }

            if (string.Equals(user.Status, UserDocument.ActiveStatus, StringComparison.Ordinal) == false)
            {
                _logger.LogWarning("[Auth] banned account attempted sign in userId = {UserId}", user.Id);

                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, BannedError);
            }

            await _experimentAssignmentService.AssignAsync(user, isNewPlayer, country, now, cancellationToken);

            var existingDevice = FindDeviceById(user, deviceIdHash);
            var previousRefreshTokenHash = existingDevice == null ? string.Empty : existingDevice.RefreshTokenHash;

            return await IssueSessionAsync(user.Id, deviceIdHash, previousRefreshTokenHash, clientVersion, country, now, cancellationToken);
        }

        public async Task<AuthSessionResult> RefreshAsync(string userId, string refreshToken, string country, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var user = await _userRepository.GetAsync(userId, cancellationToken);

            if (user == null)
                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);

            if (string.Equals(user.Status, UserDocument.ActiveStatus, StringComparison.Ordinal) == false)
                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, BannedError);

            var refreshTokenHash = _tokenGenerator.Hash(refreshToken);
            var device = FindDevice(user, refreshTokenHash);

            if (device == null)
            {
                var reason = FindPreviousDevice(user, refreshTokenHash) == null ? UnknownTokenReason : ReusedTokenReason;

                _logger.LogWarning("[Auth] refresh token rejected userId = {UserId} reason = {Reason}", userId, reason);

                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);
            }

            if (device.RefreshExpiresAt <= now)
            {
                _logger.LogInformation("[Auth] refresh token expired userId = {UserId}", userId);

                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);
            }

            await _experimentAssignmentService.AssignAsync(user, false, country, now, cancellationToken);

            return await RotateRefreshAsync(user.Id, device, country, now, cancellationToken);
        }

        private async Task<AuthSessionResult> IssueSessionAsync(string userId, string deviceIdHash, string previousRefreshTokenHash, string clientVersion, string country, DateTime now, CancellationToken cancellationToken)
        {
            var refreshToken = _tokenGenerator.CreateToken();
            var device = new UserDeviceDocument
            {
                DeviceIdHash = deviceIdHash,
                RefreshTokenHash = _tokenGenerator.Hash(refreshToken),
                PreviousRefreshTokenHash = previousRefreshTokenHash,
                RefreshExpiresAt = now.AddDays(_options.RefreshTokenDays),
                CreatedAt = now,
                LastSeenAt = now,
                ClientVersion = clientVersion,
            };

            var saved = await _userRepository.UpsertDeviceAsync(userId, device, country, now, cancellationToken);

            if (saved == false)
                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);

            return new AuthSessionResult(true, userId, refreshToken, device.RefreshExpiresAt, string.Empty);
        }

        private async Task<AuthSessionResult> RotateRefreshAsync(string userId, UserDeviceDocument device, string country, DateTime now, CancellationToken cancellationToken)
        {
            var refreshToken = _tokenGenerator.CreateToken();
            var expectedHash = device.RefreshTokenHash;
            var next = new UserDeviceDocument
            {
                DeviceIdHash = device.DeviceIdHash,
                RefreshTokenHash = _tokenGenerator.Hash(refreshToken),
                PreviousRefreshTokenHash = expectedHash,
                RefreshExpiresAt = now.AddDays(_options.RefreshTokenDays),
                CreatedAt = now,
                LastSeenAt = now,
                ClientVersion = device.ClientVersion,
            };
            var replaced = await _userRepository.TryReplaceRefreshAsync(userId, expectedHash, next, country, now, cancellationToken);

            if (replaced)
                return new AuthSessionResult(true, userId, refreshToken, next.RefreshExpiresAt, string.Empty);

            var latest = await _userRepository.GetAsync(userId, cancellationToken);

            if (latest == null)
                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);

            if (string.Equals(latest.Status, UserDocument.ActiveStatus, StringComparison.Ordinal) == false)
                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, BannedError);

            var reason = FindPreviousDevice(latest, expectedHash) == null ? UnknownTokenReason : ReusedTokenReason;

            _logger.LogWarning("[Auth] refresh token rejected userId = {UserId} reason = {Reason}", userId, reason);

            return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);
        }

        private UserDeviceDocument? FindDevice(UserDocument user, string refreshTokenHash)
        {
            for (int i = 0; i < user.Devices.Count; i++)
            {
                if (_tokenGenerator.AreEqual(user.Devices[i].RefreshTokenHash, refreshTokenHash))
                    return user.Devices[i];
            }

            return null;
        }

        private UserDeviceDocument? FindPreviousDevice(UserDocument user, string refreshTokenHash)
        {
            for (int i = 0; i < user.Devices.Count; i++)
            {
                var previous = user.Devices[i].PreviousRefreshTokenHash;

                if (previous.Length != 0 && _tokenGenerator.AreEqual(previous, refreshTokenHash))
                    return user.Devices[i];
            }

            return null;
        }

        private UserDeviceDocument? FindDeviceById(UserDocument user, string deviceIdHash)
        {
            for (int i = 0; i < user.Devices.Count; i++)
            {
                if (string.Equals(user.Devices[i].DeviceIdHash, deviceIdHash, StringComparison.Ordinal))
                    return user.Devices[i];
            }

            return null;
        }
    }
}
