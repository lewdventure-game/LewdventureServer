using Server.Infrastructure.Mongo.Runs;
using Server.Runs;

namespace Server.Api.Endpoints
{
    internal sealed class RunResponseFactory
    {
        private readonly BattleDeliveryModes _battleDeliveryModes = new();

        public RunResponse Create(RunDocument run, RunStepOutcome? step)
        {
            return Create(run, step, string.Empty);
        }

        public RunResponse Create(RunDocument run, RunStepOutcome? step, string battleDelivery)
        {
            var response = new RunResponse
            {
                RunId = run.Id,
                Status = run.Status,
                StoryLevelId = run.StoryLevelId,
                ConfigVersion = run.ConfigVersion,
                StageIndex = run.StageIndex,
                StagesTotal = run.Stages.Count,
                Experience = run.Experience,
                ExperienceLevel = run.ExperienceLevel,
                CurrentHealth = run.CurrentHealth,
                Perks = new List<int>(run.Perks),
            };

            for (int i = 0; i < run.Stages.Count; i++)
            {
                var stage = run.Stages[i];
                var visible = i <= run.StageIndex;

                response.Stages.Add(new RunStageResponse
                {
                    StageId = stage.StageId,
                    EventId = visible ? stage.EventId : 0,
                    IsBoss = stage.IsBoss,
                    Resolved = stage.Resolved,
                });
            }

            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                var bonus = run.Bonuses[i];

                response.Bonuses.Add(new RunBonusResponse
                {
                    BonusId = bonus.BonusId,
                    Count = bonus.Count,
                    RemainingBattles = bonus.RemainingBattles,
                });
            }

            if (run.PendingChoice != null)
            {
                response.PendingChoice = new RunChoiceResponse
                {
                    Kind = run.PendingChoice.Kind,
                    Options = new List<int>(run.PendingChoice.Options),
                    ChoiceCount = run.PendingChoice.ChoiceCount,
                };
            }

            if (step == null)
                return response;

            response.Step = new RunStepResponse
            {
                EventType = step.EventType,
                EventId = step.EventId,
                StageId = step.StageId,
                LocKey = step.LocKey,
                LocKeyStart = step.LocKeyStart,
                LocKeyEnd = step.LocKeyEnd,
                ExperienceGained = step.ExperienceGained,
                LevelUps = new List<int>(step.LevelUps),
                RunCompleted = step.RunCompleted,
                RunFailed = step.RunFailed,
                BattleDigest = step.BattleDigest,
                BattleStepCount = step.BattleStepCount,
                AppliedRewards = CreateRewards(step.AppliedRewards),
                ProfileRev = step.ProfileRev,
            };

            if (_battleDeliveryModes.IsSeedOnly(battleDelivery))
                response.Step.BattleInput = step.BattleInput;
            else
                response.Step.Battle = step.BattleScript;

            return response;
        }

        private List<RunRewardResponse> CreateRewards(List<RunAppliedReward> appliedRewards)
        {
            var rewards = new List<RunRewardResponse>(appliedRewards.Count);

            for (int i = 0; i < appliedRewards.Count; i++)
            {
                var applied = appliedRewards[i];

                rewards.Add(new RunRewardResponse
                {
                    Type = applied.Type,
                    Id = applied.Id,
                    RewardKey = applied.RewardKey,
                    Count = applied.Count,
                });
            }

            return rewards;
        }
    }
}
