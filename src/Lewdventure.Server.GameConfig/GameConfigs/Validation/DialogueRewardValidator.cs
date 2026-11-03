using System.Globalization;
using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class DialogueRewardValidator
    {
        private const string DialogueEntry = "dialogue";
        private const string DialoguePrefix = "dialogue:";

        public void Collect(IConfigDistributor distributor, List<string> errors)
        {
            CollectCharacterPromotes(distributor, errors);
            CollectSummonMasteries(distributor, errors);
            CollectPackedRewards(distributor, errors);
        }

        private void CollectCharacterPromotes(IConfigDistributor distributor, List<string> errors)
        {
            var promotes = distributor.CharacterPromotes.Collection;

            for (int i = 0; i < promotes.Count; i++)
            {
                var promote = promotes[i];
                var place = $"Лист Character_promotes, набор {promote.Id}, уровень {promote.PromoteLevel}";

                CollectLevelRewards(promote.RewardTypes, promote.RewardIds, promote.RewardValues, place, distributor, errors);
            }
        }

        private void CollectSummonMasteries(IConfigDistributor distributor, List<string> errors)
        {
            var masteries = distributor.SummonMasteries.Collection;

            for (int i = 0; i < masteries.Count; i++)
            {
                var mastery = masteries[i];
                var place = $"Лист Summon_masteries, набор {mastery.Id}, уровень {mastery.MasteryLevel}";

                CollectLevelRewards(mastery.RewardTypes, mastery.RewardIds, mastery.RewardValues, place, distributor, errors);
            }
        }

        private void CollectLevelRewards(
            string[] types,
            string[] ids,
            int[] values,
            string place,
            IConfigDistributor distributor,
            List<string> errors)
        {
            for (int i = 0; i < types.Length; i++)
            {
                if (string.Equals(types[i], DialogueEntry, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                if (ids.Length <= i || values.Length <= i)
                {
                    errors.Add($"{place}: награда dialogue не имеет пары в reward_ids и reward_values.");

                    continue;
                }

                if (int.TryParse(ids[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dialogueId) == false)
                {
                    errors.Add($"{place}: id диалога «{ids[i]}» не число.");

                    continue;
                }

                Validate(dialogueId, values[i], $"dialogue:{ids[i]}:{values[i]}", place, distributor, errors);
            }
        }

        private void CollectPackedRewards(IConfigDistributor distributor, List<string> errors)
        {
            var events = distributor.StoryEvents.Collection;

            for (int i = 0; i < events.Count; i++)
                CollectPackedValue(events[i].EventParameters, $"Лист Story_events, событие {events[i].Id}", distributor, errors);

            var constants = distributor.Constants.Collection;

            for (int i = 0; i < constants.Count; i++)
                CollectPackedValue(constants[i].ConstantValue, $"Лист Constants, {constants[i].ConstantName}", distributor, errors);
        }

        private void CollectPackedValue(string value, string place, IConfigDistributor distributor, List<string> errors)
        {
            var searchFrom = 0;

            while (searchFrom < value.Length)
            {
                var start = value.IndexOf(DialoguePrefix, searchFrom, StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                    return;

                var end = start + DialoguePrefix.Length;

                while (end < value.Length && IsEntryTerminator(value[end]) == false)
                    end += 1;

                var entry = value.Substring(start, end - start);
                var parts = entry.Split(':');

                if (parts.Length != 3
                    || int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dialogueId) == false
                    || int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) == false)
                {
                    errors.Add($"{place}: награда «{entry}» не читается, нужен формат dialogue:<id>:<количество>.");
                }
                else
                {
                    Validate(dialogueId, count, entry, place, distributor, errors);
                }

                searchFrom = end;
            }
        }

        private bool IsEntryTerminator(char character)
        {
            return character == ',' || character == ';' || character == ']' || character == '}' || char.IsWhiteSpace(character);
        }

        private void Validate(int dialogueId, int count, string entry, string place, IConfigDistributor distributor, List<string> errors)
        {
            if (dialogueId <= 0 || count <= 0)
            {
                errors.Add($"{place}: награда {entry} некорректна, id и количество должны быть больше нуля.");

                return;
            }

            if (distributor.Dialogues.Contains(dialogueId) == false)
                errors.Add($"{place}: награда {entry} ссылается на диалог {dialogueId}, которого нет в листе Dialogues.");
        }
    }
}
