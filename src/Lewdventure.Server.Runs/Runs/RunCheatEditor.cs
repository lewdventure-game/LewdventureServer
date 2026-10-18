using Server.Bonuses;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Runs
{
    internal sealed class RunCheatEditor
    {
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly RewardApplier _rewardApplier;

        public RunCheatEditor(IBonusWorkModeParser bonusWorkModeParser, RewardApplier rewardApplier)
        {
            _bonusWorkModeParser = bonusWorkModeParser;
            _rewardApplier = rewardApplier;
        }

        public string Apply(RunDocument run, RunCheatCommand command, IConfigDistributor configDistributor)
        {
            if (run.PendingBattle != null && IsBlockedByPendingBattle(command.Action))
                return "Run waits for battle finish: the battle is already calculated, so this change would be lost or applied twice. Clear the pending battle or let the client finish it first.";

            switch (command.Action)
            {
                case RunCheatCommand.StageAction:
                    return SetStage(run, command.Stage);
                case RunCheatCommand.EventAction:
                    return SetStageEvent(run, command.Stage, command.Id, configDistributor);
                case RunCheatCommand.HealthAction:
                    return SetHealth(run, command.Value);
                case RunCheatCommand.AddPerkAction:
                    return AddPerk(run, command.Id, configDistributor);
                case RunCheatCommand.RemovePerkAction:
                    return RemovePerk(run, command.Id);
                case RunCheatCommand.AddBonusAction:
                    return AddBonus(run, command.Id, command.Count, command.Battles, configDistributor);
                case RunCheatCommand.AddStatusAction:
                    return AddStatus(run, command.Id, configDistributor);
                case RunCheatCommand.ClearPerksAction:
                    run.Perks.Clear();
                    run.PerkUsages.Clear();

                    return string.Empty;
                case RunCheatCommand.ClearBonusesAction:
                    run.Bonuses.Clear();

                    return string.Empty;
                case RunCheatCommand.ClearStatusesAction:
                    run.Statuses.Clear();

                    return string.Empty;
                case RunCheatCommand.ClearBattleAction:
                    return ClearBattle(run);
                case RunCheatCommand.AddPerkGroupAction:
                    return AddPerkGroup(run, command.Id, command.Count, configDistributor);
                default:
                    return $"Unknown run cheat action {command.Action}.";
            }
        }

        private bool IsBlockedByPendingBattle(string action)
        {
            return string.Equals(action, RunCheatCommand.StageAction, StringComparison.Ordinal) == false
                && string.Equals(action, RunCheatCommand.EventAction, StringComparison.Ordinal) == false
                && string.Equals(action, RunCheatCommand.ClearBattleAction, StringComparison.Ordinal) == false
                && string.Equals(action, RunCheatCommand.AddPerkGroupAction, StringComparison.Ordinal) == false;
        }

        private string ClearBattle(RunDocument run)
        {
            if (run.PendingBattle == null)
                return "Run has no pending battle.";

            run.PendingBattle = null;

            return string.Empty;
        }

        private string AddPerkGroup(RunDocument run, int groupId, int count, IConfigDistributor configDistributor)
        {
            if (configDistributor.PerkGroups.TryGet(groupId, out _) == false)
                return $"Perk group {groupId} is missing in configs of the run.";

            if (count < 1)
                return "Perk group count must be at least 1.";

            for (int i = 0; i < count; i++)
                run.PendingPerkGroups.Add(groupId);

            return string.Empty;
        }

        private string SetStage(RunDocument run, int stageNumber)
        {
            if (stageNumber < 1 || run.Stages.Count < stageNumber)
                return $"Stage must be from 1 to {run.Stages.Count}.";

            var index = stageNumber - 1;

            for (int i = 0; i < run.Stages.Count; i++)
                run.Stages[i].Resolved = i < index;

            run.StageIndex = index;
            run.PendingBattle = null;

            if (run.PendingChoice != null && string.Equals(run.PendingChoice.Kind, RunPendingChoiceDocument.ForkKind, StringComparison.Ordinal))
                run.PendingChoice = null;

            return string.Empty;
        }

        private string SetStageEvent(RunDocument run, int stageNumber, int eventId, IConfigDistributor configDistributor)
        {
            if (stageNumber < 1 || run.Stages.Count < stageNumber)
                return $"Stage must be from 1 to {run.Stages.Count}.";

            if (configDistributor.StoryEvents.TryGet(eventId, out _) == false)
                return $"Story event {eventId} is missing in configs of the run.";

            var index = stageNumber - 1;

            if (index < run.StageIndex)
                return $"Stage {stageNumber} is already passed; move the run back to it first.";

            if (index == run.StageIndex)
                run.PendingBattle = null;

            if (index == run.StageIndex && run.PendingChoice != null && string.Equals(run.PendingChoice.Kind, RunPendingChoiceDocument.ForkKind, StringComparison.Ordinal))
                run.PendingChoice = null;

            run.Stages[index].EventId = eventId;
            run.Stages[index].Resolved = false;

            return string.Empty;
        }

        private string SetHealth(RunDocument run, float health)
        {
            if (health <= 0f || float.IsFinite(health) == false)
                return "Health must be greater than 0.";

            run.CurrentHealth = health;

            return string.Empty;
        }

        private string AddPerk(RunDocument run, int perkId, IConfigDistributor configDistributor)
        {
            if (configDistributor.Perks.TryGet(perkId, out _) == false)
                return $"Perk {perkId} is missing in configs of the run.";

            run.Perks.Add(perkId);

            return string.Empty;
        }

        private string RemovePerk(RunDocument run, int perkId)
        {
            if (run.Perks.Remove(perkId) == false)
                return $"Run has no perk {perkId}.";

            if (run.Perks.Contains(perkId))
                return string.Empty;

            for (int i = run.PerkUsages.Count - 1; 0 <= i; i--)
            {
                if (run.PerkUsages[i].PerkId == perkId)
                    run.PerkUsages.RemoveAt(i);
            }

            return string.Empty;
        }

        private string AddBonus(RunDocument run, int bonusId, int count, int battles, IConfigDistributor configDistributor)
        {
            if (configDistributor.Bonuses.TryGet(bonusId, out var bonusMapper) == false)
                return $"Bonus {bonusId} is missing in configs of the run.";

            if (_bonusWorkModeParser.TryParse(bonusMapper.WorkModeParameters, out var workMode) && _rewardApplier.IsAccountScoped(workMode))
                return $"Bonus {bonusId} is an account bonus ({bonusMapper.WorkModeParameters}): in the game it goes to the profile, grant it with the account reward instead.";

            if (count < 1)
                return "Bonus count must be at least 1.";

            if (battles < 0)
                return "Bonus battles cannot be negative.";

            run.Bonuses.Add(new RunBonusDocument
            {
                BonusId = bonusId,
                Count = count,
                RemainingBattles = battles,
            });

            return string.Empty;
        }

        private string AddStatus(RunDocument run, int statusId, IConfigDistributor configDistributor)
        {
            if (configDistributor.Statuses.TryGet(statusId, out _) == false)
                return $"Status {statusId} is missing in configs of the run.";

            run.Statuses.Add(statusId);

            return string.Empty;
        }
    }
}
