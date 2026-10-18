using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Mongo.Runs;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class QaResponseFactory
    {
        public QaRunResponse CreateRun(RunDocument run, IConfigDistributor configDistributor)
        {
            var response = new QaRunResponse
            {
                RunId = run.Id,
                StoryLevelId = run.StoryLevelId,
                ConfigVersion = run.ConfigVersion,
                Status = run.Status,
                StageNumber = run.StageIndex + 1,
                CurrentHealth = run.CurrentHealth,
                MaxHealth = run.CurrentHealthMaxReference,
                Experience = run.Experience,
                ExperienceLevel = run.ExperienceLevel,
                PendingLevelUps = run.PendingLevelUps,
                PendingChoice = run.PendingChoice == null ? string.Empty : run.PendingChoice.Kind,
                PendingChoiceOptions = run.PendingChoice == null ? new List<int>() : new List<int>(run.PendingChoice.Options),
                PendingStartPerkGroups = new List<int>(run.PendingStartPerkGroups),
                PendingPerkGroups = new List<int>(run.PendingPerkGroups),
                PendingBattle = CreatePendingBattle(run.PendingBattle),
                Perks = new List<int>(run.Perks),
                Statuses = new List<int>(run.Statuses),
            };

            for (int i = 0; i < run.Stages.Count; i++)
            {
                var stage = run.Stages[i];
                var stageResponse = new QaRunStageResponse
                {
                    Number = i + 1,
                    StageId = stage.StageId,
                    EventId = stage.EventId,
                    IsBoss = stage.IsBoss,
                    Resolved = stage.Resolved,
                    IsCurrent = i == run.StageIndex,
                };

                if (configDistributor.StoryEvents.TryGet(stage.EventId, out var storyEvent))
                    stageResponse.EventType = storyEvent.EventType.ToString();

                if (configDistributor.StoryStages.TryGet(stage.StageId, out var stageMapper))
                    stageResponse.CandidateEventIds.AddRange(stageMapper.EventIds);

                response.Stages.Add(stageResponse);
            }

            for (int i = 0; i < run.Bonuses.Count; i++)
            {
                response.Bonuses.Add(new RunBonusResponse
                {
                    BonusId = run.Bonuses[i].BonusId,
                    Count = run.Bonuses[i].Count,
                    RemainingBattles = run.Bonuses[i].RemainingBattles,
                });
            }

            return response;
        }

        public QaTemplateResponse CreateTemplate(QaTemplateDocument template)
        {
            var profile = template.Profile;
            var levels = profile.Story.CompletedLevelIds.Count == 0 ? "нет" : string.Join(", ", profile.Story.CompletedLevelIds);

            return new QaTemplateResponse
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                CreatedBy = template.CreatedBy,
                SourceUserId = template.SourceUserId,
                ConfigVersion = template.ConfigVersion,
                UpdatedAt = template.UpdatedAt,
                Summary = $"персонажей {profile.Characters.Count}, саммонов {profile.Summons.Count}, снаряжения {profile.Equipment.Count}, ресурсов {profile.Resources.Count}, бонусов аккаунта {profile.Bonuses.Count}, пройдены уровни: {levels}",
            };
        }

        private QaPendingBattleResponse? CreatePendingBattle(RunPendingBattleDocument? pending)
        {
            if (pending == null)
                return null;

            var response = new QaPendingBattleResponse
            {
                StageNumber = pending.StageIndex + 1,
                EventId = pending.EventId,
                IsWin = pending.IsWin,
                Outcome = pending.Outcome,
                FinalHealth = pending.FinalHealth,
                StepCount = pending.BattleStepCount,
            };

            for (int i = 0; i < pending.BonusGrants.Count; i++)
            {
                var grant = pending.BonusGrants[i];

                response.BonusGrants.Add(new RunBonusResponse { BonusId = grant.BonusId, Count = grant.Count, RemainingBattles = grant.RemainingBattles });
            }

            for (int i = 0; i < pending.PerkUsages.Count; i++)
            {
                var usage = pending.PerkUsages[i];

                response.PerkUsages.Add(new QaPerkUsageResponse { PerkId = usage.PerkId, UsedCount = usage.UsedCount, RemainingUses = usage.RemainingUses });
            }

            return response;
        }
    }
}
