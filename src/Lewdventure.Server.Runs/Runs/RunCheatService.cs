using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Mongo.Runs;

namespace Server.Runs
{
    internal sealed class RunCheatService
    {
        private const int MaxAttempts = 3;

        private readonly GameConfigSetCache _gameConfigSetCache;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly ILogger<RunCheatService> _logger;
        private readonly RunCheatEditor _runCheatEditor;
        private readonly RunRepository _runRepository;
        private readonly TimeProvider _timeProvider;

        public RunCheatService(
            GameConfigSetCache gameConfigSetCache,
            IGameConfigSetProvider gameConfigSetProvider,
            ILogger<RunCheatService> logger,
            RunCheatEditor runCheatEditor,
            RunRepository runRepository,
            TimeProvider timeProvider)
        {
            _gameConfigSetCache = gameConfigSetCache;
            _gameConfigSetProvider = gameConfigSetProvider;
            _logger = logger;
            _runCheatEditor = runCheatEditor;
            _runRepository = runRepository;
            _timeProvider = timeProvider;
        }

        public async Task<GameConfigSet> ResolveConfigsAsync(RunDocument run, CancellationToken cancellationToken)
        {
            var configSet = await _gameConfigSetCache.GetAsync(run.ConfigVersion, cancellationToken);

            return configSet == null ? _gameConfigSetProvider.Current : configSet;
        }

        public async Task<RunOperationResult> ApplyAsync(string userId, RunCheatCommand command, string actor, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var run = await _runRepository.GetActiveAsync(userId, cancellationToken);

                if (run == null)
                    return new RunOperationResult(null, null, false, new List<string> { "Player has no active run." });

                var configSet = await ResolveConfigsAsync(run, cancellationToken);
                var expectedRev = run.Rev;
                var error = _runCheatEditor.Apply(run, command, configSet.Distributor);

                if (error.Length != 0)
                    return new RunOperationResult(null, null, false, new List<string> { error });

                run.Rev = expectedRev + 1;
                run.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (await _runRepository.ReplaceAsync(run, expectedRev, cancellationToken) == false)
                    continue;

                _logger.LogInformation(
                    "[Cheat] run changed userId = {UserId} runId = {RunId} actor = {Actor} action = {Action} stage = {Stage} id = {Id} value = {Value} count = {Count} battles = {Battles}",
                    userId,
                    run.Id,
                    actor,
                    command.Action,
                    command.Stage,
                    command.Id,
                    command.Value,
                    command.Count,
                    command.Battles);

                return new RunOperationResult(run, null, false, new List<string>());
            }

            _logger.LogWarning("[Cheat] run conflict userId = {UserId} actor = {Actor} action = {Action}", userId, actor, command.Action);

            return new RunOperationResult(null, null, true, new List<string>());
        }
    }
}
