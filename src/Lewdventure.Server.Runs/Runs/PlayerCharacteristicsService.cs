using Server.Battles;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Runs
{
    internal sealed class PlayerCharacteristicsService
    {
        private readonly IConfigDistributor _configDistributor;
        private readonly PlayerProfileService _playerProfileService;
        private readonly RunRepository _runRepository;
        private readonly RunSnapshotBuilder _runSnapshotBuilder;
        private readonly IUnitStateBuilder _unitStateBuilder;

        public PlayerCharacteristicsService(
            IConfigDistributor configDistributor,
            PlayerProfileService playerProfileService,
            RunRepository runRepository,
            RunSnapshotBuilder runSnapshotBuilder,
            IUnitStateBuilder unitStateBuilder)
        {
            _configDistributor = configDistributor;
            _playerProfileService = playerProfileService;
            _runRepository = runRepository;
            _runSnapshotBuilder = runSnapshotBuilder;
            _unitStateBuilder = unitStateBuilder;
        }

        public async Task<PlayerCharacteristicsResult> GetAsync(string userId, CancellationToken cancellationToken)
        {
            var profile = await _playerProfileService.GetOrCreateAsync(userId, cancellationToken);
            var characterId = profile.Loadout.CharacterId;

            if (characterId <= 0)
                return new PlayerCharacteristicsResult(null, "Loadout has no character.");

            if (_runSnapshotBuilder.FindCharacter(profile, characterId) == null)
                return new PlayerCharacteristicsResult(null, $"Character {characterId} is not unlocked.");

            if (_configDistributor.Characters.TryGet(characterId, out _) == false)
                return new PlayerCharacteristicsResult(null, $"Character {characterId} is missing in configs.");

            var run = await _runRepository.GetActiveAsync(userId, cancellationToken);
            var unit = _runSnapshotBuilder.BuildMainUnit(profile, run ?? CreateEmptyRun(userId), ResolveCharacter(profile, characterId));
            var storyLevelId = run == null ? 0 : run.StoryLevelId;
            var stageId = ResolveStageId(run);
            var unitState = _unitStateBuilder.Build(unit, BattleSide.Attacking, false, storyLevelId, stageId);

            return new PlayerCharacteristicsResult(unitState.CharacteristicState, string.Empty);
        }

        private PlayerCharacterDocument ResolveCharacter(PlayerProfileDocument profile, int characterId)
        {
            return _runSnapshotBuilder.FindCharacter(profile, characterId)!;
        }

        private RunDocument CreateEmptyRun(string userId)
        {
            return new RunDocument { UserId = userId };
        }

        private int ResolveStageId(RunDocument? run)
        {
            if (run == null || run.Stages.Count <= run.StageIndex)
                return 0;

            return run.Stages[run.StageIndex].StageId;
        }
    }
}
