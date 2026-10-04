using Microsoft.Extensions.Options;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerAuthService
    {
        private const string UserIdPrefix = "usr_";
        private const string BannedError = "Account is banned.";
        private const string UnknownSessionError = "Refresh token is not valid.";

        private readonly ExperimentAssignmentService _experimentAssignmentService;
        private readonly ILogger<PlayerAuthService> _logger;
        private readonly AuthOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly TokenGenerator _tokenGenerator;
        private readonly UserRepository _userRepository;

        public PlayerAuthService(
            ExperimentAssignmentService experimentAssignmentService,
            ILogger<PlayerAuthService> logger,
            IOptions<AuthOptions> options,
            TimeProvider timeProvider,
            TokenGenerator tokenGenerator,
            UserRepository userRepository)
        {
            _experimentAssignmentService = experimentAssignmentService;
            _logger = logger;
            _options = options.Value;
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
            }

            if (string.Equals(user.Status, UserDocument.ActiveStatus, StringComparison.Ordinal) == false)
            {
                _logger.LogWarning("[Auth] banned account attempted sign in userId = {UserId}", user.Id);

                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, BannedError);
            }

            await _experimentAssignmentService.AssignAsync(user, isNewPlayer, country, now, cancellationToken);

            return await IssueSessionAsync(user.Id, deviceIdHash, clientVersion, country, now, cancellationToken);
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
                _logger.LogWarning("[Auth] refresh token rejected userId = {UserId}", userId);

                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);
            }

            if (device.RefreshExpiresAt <= now)
            {
                _logger.LogInformation("[Auth] refresh token expired userId = {UserId}", userId);

                return new AuthSessionResult(false, string.Empty, string.Empty, DateTime.MinValue, UnknownSessionError);
            }

            await _experimentAssignmentService.AssignAsync(user, false, country, now, cancellationToken);

            return await IssueSessionAsync(user.Id, device.DeviceIdHash, device.ClientVersion, country, now, cancellationToken);
        }

        private async Task<AuthSessionResult> IssueSessionAsync(string userId, string deviceIdHash, string clientVersion, string country, DateTime now, CancellationToken cancellationToken)
        {
            var refreshToken = _tokenGenerator.CreateToken();
            var device = new UserDeviceDocument
            {
                DeviceIdHash = deviceIdHash,
                RefreshTokenHash = _tokenGenerator.Hash(refreshToken),
                RefreshExpiresAt = now.AddDays(_options.RefreshTokenDays),
                CreatedAt = now,
                LastSeenAt = now,
                ClientVersion = clientVersion,
            };

            await _userRepository.UpsertDeviceAsync(userId, device, country, now, cancellationToken);

            return new AuthSessionResult(true, userId, refreshToken, device.RefreshExpiresAt, string.Empty);
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
    }
}
