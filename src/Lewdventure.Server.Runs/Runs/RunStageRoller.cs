using Server.Infrastructure.Mongo.Runs;
using Server.Services;
using Server.Stories;

namespace Server.Runs
{
    internal sealed class RunStageRoller
    {
        private readonly RunRandomFactory _runRandomFactory;

        public RunStageRoller(RunRandomFactory runRandomFactory)
        {
            _runRandomFactory = runRandomFactory;
        }

        public RunStageRoll Roll(IStoryLevelMapper level, IConfigDistributor configDistributor, long seed, int rollIndex)
        {
            var stages = new List<RunStageDocument>(level.StageIds.Length);
            var errors = new List<string>();

            for (int i = 0; i < level.StageIds.Length; i++)
            {
                var stageId = level.StageIds[i];

                if (configDistributor.StoryStages.TryGet(stageId, out var stage) == false)
                {
                    errors.Add($"Story stage {stageId} is missing in configs.");

                    continue;
                }

                if (stage.EventIds.Length == 0)
                {
                    errors.Add($"Story stage {stageId} has no events.");

                    continue;
                }

                var random = _runRandomFactory.Create(seed, rollIndex);

                rollIndex += 1;

                var eventId = _runRandomFactory.PickWeighted(random, stage.EventIds, stage.EventChances);

                if (configDistributor.StoryEvents.TryGet(eventId, out _) == false)
                {
                    errors.Add($"Story event {eventId} from stage {stageId} is missing in configs.");

                    continue;
                }

                stages.Add(new RunStageDocument
                {
                    StageId = stageId,
                    EventId = eventId,
                    IsBoss = stage.IsBossLevel,
                });
            }

            return new RunStageRoll(stages, rollIndex, errors);
        }
    }
}
