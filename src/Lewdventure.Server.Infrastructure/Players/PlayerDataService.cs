using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerDataService
    {
        private const int ExportLimit = 200;

        private readonly IdempotencyRepository _idempotencyRepository;
        private readonly ILogger<PlayerDataService> _logger;
        private readonly PlayerLedgerRepository _playerLedgerRepository;
        private readonly PlayerProfileRepository _playerProfileRepository;
        private readonly RunRepository _runRepository;
        private readonly UserRepository _userRepository;

        public PlayerDataService(
            IdempotencyRepository idempotencyRepository,
            ILogger<PlayerDataService> logger,
            PlayerLedgerRepository playerLedgerRepository,
            PlayerProfileRepository playerProfileRepository,
            RunRepository runRepository,
            UserRepository userRepository)
        {
            _idempotencyRepository = idempotencyRepository;
            _logger = logger;
            _playerLedgerRepository = playerLedgerRepository;
            _playerProfileRepository = playerProfileRepository;
            _runRepository = runRepository;
            _userRepository = userRepository;
        }

        public async Task<PlayerExport> ExportAsync(string userId, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetAsync(userId, cancellationToken);
            var profile = await _playerProfileRepository.GetAsync(userId, cancellationToken);
            var runs = await _runRepository.ListAsync(userId, ExportLimit, cancellationToken);
            var ledger = await _playerLedgerRepository.ListAsync(userId, ExportLimit, cancellationToken);
            var devices = user == null ? 0 : user.Devices.Count;
            var createdAt = user == null ? DateTime.MinValue : user.CreatedAt;
            var status = user == null ? string.Empty : user.Status;

            return new PlayerExport(userId, status, createdAt, devices, profile, runs, ledger);
        }

        public async Task<PlayerDeletion> ResetProgressAsync(string userId, string actor, CancellationToken cancellationToken)
        {
            var runs = await _runRepository.DeleteByUserAsync(userId, cancellationToken);
            var ledger = await _playerLedgerRepository.DeleteByUserAsync(userId, cancellationToken);
            var idempotency = await _idempotencyRepository.DeleteByUserAsync(userId, cancellationToken);
            var profiles = await _playerProfileRepository.DeleteAsync(userId, cancellationToken);

            _logger.LogWarning(
                "[Player] progress reset userId = {UserId} actor = {Actor} profile = {Profiles} runs = {Runs} ledger = {Ledger} idempotency = {Idempotency}",
                userId,
                actor,
                profiles,
                runs,
                ledger,
                idempotency);

            return new PlayerDeletion(profiles, runs, ledger, idempotency, 0);
        }

        public async Task<PlayerDeletion> DeleteAsync(string userId, string actor, CancellationToken cancellationToken)
        {
            var runs = await _runRepository.DeleteByUserAsync(userId, cancellationToken);
            var ledger = await _playerLedgerRepository.DeleteByUserAsync(userId, cancellationToken);
            var idempotency = await _idempotencyRepository.DeleteByUserAsync(userId, cancellationToken);
            var profiles = await _playerProfileRepository.DeleteAsync(userId, cancellationToken);
            var users = await _userRepository.DeleteAsync(userId, cancellationToken);

            _logger.LogWarning(
                "[Player] data deleted userId = {UserId} actor = {Actor} profile = {Profiles} runs = {Runs} ledger = {Ledger} idempotency = {Idempotency} user = {Users}",
                userId,
                actor,
                profiles,
                runs,
                ledger,
                idempotency,
                users);

            return new PlayerDeletion(profiles, runs, ledger, idempotency, users);
        }
    }
}
