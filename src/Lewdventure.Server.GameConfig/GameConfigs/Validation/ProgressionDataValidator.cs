using System.Globalization;
using Server.Bonuses;
using Server.Equipments;
using Server.Services;
using Server.Stories;

namespace Server.GameConfigs
{
    internal sealed class ProgressionDataValidator
    {
        private const string StartPerkGroupsConstant = "start_perk_groups";
        private const string EnergyMaxBaseConstant = "energy_max_base";
        private const string EquipmentLevelDropResourceConstant = "equipment_lvl_drop_resource";
        private const string SummonLevelResetResourceConstant = "summon_level_reset_resource";

        public void Collect(IConfigDistributor distributor, List<string> warnings)
        {
            CollectSummonDamageLevels(distributor, warnings);
            CollectEquipmentBonusLevels(distributor, warnings);
            CollectStartPerkGroups(distributor, warnings);
            CollectCharacterPromotes(distributor, warnings);
            CollectCharacterSkillLevels(distributor, warnings);
            CollectStoryReferences(distributor, warnings);
            CollectEquipmentMergeChains(distributor, warnings);
            CollectSummonSkillLevels(distributor, warnings);
            CollectEquipmentBonusLinks(distributor, warnings);
            CollectPassiveHealingBonuses(distributor, warnings);
            CollectEnergyThresholds(distributor, warnings);
            CollectResourceCostConstant(distributor, EquipmentLevelDropResourceConstant, warnings);
            CollectResourceCostConstant(distributor, SummonLevelResetResourceConstant, warnings);
        }

        private void CollectResourceCostConstant(IConfigDistributor distributor, string constantName, List<string> warnings)
        {
            if (distributor.Constants.TryGet(constantName, out var constant) == false || string.IsNullOrWhiteSpace(constant.ConstantValue))
                return;

            var entries = constant.ConstantValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < entries.Length; i++)
            {
                var tokens = entries[i].Trim().Split(':');

                if (tokens.Length == 3
                    && string.Equals(tokens[0].Trim(), "resource", StringComparison.OrdinalIgnoreCase)
                    && tokens[1].Trim().Length != 0
                    && int.TryParse(tokens[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount)
                    && 0 < amount)
                    continue;

                warnings.Add($"Лист Constants, {constantName}: запись «{entries[i].Trim()}» не читается, ожидается resource:ключ:количество. Нечитаемая стоимость делает операцию бесплатной.");
            }
        }

        private void CollectSummonSkillLevels(IConfigDistributor distributor, List<string> warnings)
        {
            var summons = distributor.Summons.Collection;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];

                for (int skillIndex = 0; skillIndex < summon.SkillIds.Length && skillIndex < summon.SkillUpgradeIds.Length; skillIndex++)
                {
                    if (int.TryParse(summon.SkillIds[skillIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var skillId) == false)
                        continue;

                    if (distributor.Skills.TryGet(skillId, out var skill) == false)
                        continue;

                    var values = Math.Max(CountMaxArguments(skill.Triggers), CountMaxArguments(skill.Actions));
                    var levels = ReadMaxSkillPromoteLevel(distributor, summon.SkillUpgradeIds[skillIndex]);

                    if (values <= 1 || values == levels)
                        continue;

                    warnings.Add($"Лист Summons, саммон {summon.Id}: у скилла {skillId} по {values} значений на параметр, а уровней в Skill_promotes (паттерн {summon.SkillUpgradeIds[skillIndex]}) до {levels}. По ГДД количество должно совпадать.");
                }
            }
        }

        private int ReadMaxSkillPromoteLevel(IConfigDistributor distributor, int patternId)
        {
            var promotes = distributor.SkillPromotes.Collection;
            var maxLevel = 1;

            for (int i = 0; i < promotes.Count; i++)
            {
                if (promotes[i].PatternId == patternId && maxLevel < promotes[i].Level)
                    maxLevel = promotes[i].Level;
            }

            return maxLevel;
        }

        private void CollectEquipmentBonusLinks(IConfigDistributor distributor, List<string> warnings)
        {
            var equipments = distributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var equipment = equipments[i];

                for (int bonusIndex = 0; bonusIndex < equipment.BonusIds.Length; bonusIndex++)
                {
                    var bonusId = equipment.BonusIds[bonusIndex];

                    if (distributor.Bonuses.TryGet(bonusId, out var bonus) == false)
                        continue;

                    if (IsHealingBonus(bonus.BonusType))
                        warnings.Add($"Лист Equipments, предмет {equipment.Id}: бонус {bonusId} — лечение ({bonus.BonusType}), в бонусах снаряжения он ничего не сделает. Лечение работает только как награда.");

                    if (TryReadEquippedId(bonus.WorkModeParameters, out var equippedId) && equippedId != equipment.Id)
                        warnings.Add($"Лист Equipments, предмет {equipment.Id}: бонус {bonusId} привязан к if_equipped предмета {equippedId}, а не к самому предмету. На этом предмете он не включится.");
                }
            }
        }

        private bool TryReadEquippedId(string workModes, out int equipmentId)
        {
            equipmentId = 0;

            if (string.IsNullOrWhiteSpace(workModes))
                return false;

            var parts = workModes.Split(';', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                var tokens = parts[i].Trim().Split(':');

                if (tokens.Length < 3 || string.Equals(tokens[0].Trim(), "if_equipped", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                var entity = tokens[1].Trim();

                if (string.Equals(entity, "equipment", StringComparison.OrdinalIgnoreCase) == false && string.Equals(entity, "equipments", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                return int.TryParse(tokens[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out equipmentId);
            }

            return false;
        }

        private void CollectPassiveHealingBonuses(IConfigDistributor distributor, List<string> warnings)
        {
            var skills = distributor.Skills.Collection;
            var bonusIds = new List<int>();

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];

                if (skill.Triggers.Contains("as_bonus_logic", StringComparison.Ordinal) == false)
                    continue;

                bonusIds.Clear();
                CollectParameterIntegers(skill.Actions, "bonus_id", bonusIds);

                for (int bonusIndex = 0; bonusIndex < bonusIds.Count; bonusIndex++)
                {
                    if (distributor.Bonuses.TryGet(bonusIds[bonusIndex], out var bonus) && IsHealingBonus(bonus.BonusType))
                        warnings.Add($"Лист Skills, скилл {skill.Id}: пассивный скилл (as_bonus_logic) выдаёт лечение бонусом {bonusIds[bonusIndex]}, оно ничего не сделает. Лечение работает только как награда или действие скилла в бою.");
                }
            }
        }

        private void CollectEnergyThresholds(IConfigDistributor distributor, List<string> warnings)
        {
            if (distributor.Constants.TryGet(EnergyMaxBaseConstant, out var constant) == false)
                return;

            if (float.TryParse(constant.ConstantValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var energyMax) == false || energyMax <= 0f)
                return;

            var checkedSkills = new HashSet<int>();
            var characters = distributor.Characters.Collection;

            for (int i = 0; i < characters.Count; i++)
                CollectEnergyThresholds(distributor, characters[i].SkillIds, energyMax, checkedSkills, warnings);

            var equipments = distributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
                CollectEnergyThresholds(distributor, equipments[i].SkillIds, energyMax, checkedSkills, warnings);
        }

        private void CollectEnergyThresholds(IConfigDistributor distributor, int[] skillIds, float energyMax, HashSet<int> checkedSkills, List<string> warnings)
        {
            var thresholds = new List<int>();

            for (int i = 0; i < skillIds.Length; i++)
            {
                if (checkedSkills.Add(skillIds[i]) == false || distributor.Skills.TryGet(skillIds[i], out var skill) == false)
                    continue;

                if (skill.Triggers.Contains("energy_needed", StringComparison.Ordinal) == false)
                    continue;

                thresholds.Clear();
                CollectParameterIntegers(skill.Triggers, "energy", thresholds);

                for (int thresholdIndex = 0; thresholdIndex < thresholds.Count; thresholdIndex++)
                {
                    if (thresholds[thresholdIndex] <= energyMax)
                        continue;

                    warnings.Add($"Лист Skills, скилл {skill.Id}: порог энергии {thresholds[thresholdIndex]} больше {EnergyMaxBaseConstant} = {energyMax.ToString(CultureInfo.InvariantCulture)}, у персонажа энергия столько не накопится и скилл не сработает.");

                    break;
                }
            }
        }

        private void CollectParameterIntegers(string text, string parameterName, List<int> values)
        {
            var marker = parameterName + ":{";
            var start = text.IndexOf(marker, StringComparison.Ordinal);

            while (0 <= start)
            {
                var open = start + marker.Length;
                var close = text.IndexOf('}', open);

                if (close < 0)
                    return;

                var tokens = text.Substring(open, close - open).Split(new[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < tokens.Length; i++)
                {
                    if (float.TryParse(tokens[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        values.Add((int)MathF.Ceiling(value));
                }

                start = text.IndexOf(marker, close, StringComparison.Ordinal);
            }
        }

        private bool IsHealingBonus(BonusType bonusType)
        {
            return bonusType == BonusType.Healing || bonusType == BonusType.HealingFromMax || bonusType == BonusType.CurrentHealthLocal;
        }

        private void CollectCharacterPromotes(IConfigDistributor distributor, List<string> warnings)
        {
            var characters = distributor.Characters.Collection;

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];

                if (distributor.CharacterPromotes.TryGet(character.PromoteId, 1, out _))
                    continue;

                warnings.Add($"Лист Characters, персонаж {character.Id}: в Character_promotes у паттерна {character.PromoteId} нет строки promote_level = 1. Новый персонаж останется на уровне 0 и не получит наград первого уровня.");
            }
        }

        private void CollectCharacterSkillLevels(IConfigDistributor distributor, List<string> warnings)
        {
            var characters = distributor.Characters.Collection;

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                var promoteLevels = character.PromoteToSkillLevels.Length;

                for (int skillIndex = 0; skillIndex < character.SkillIds.Length; skillIndex++)
                {
                    var skillId = character.SkillIds[skillIndex];

                    if (distributor.Skills.TryGet(skillId, out var skill) == false)
                        continue;

                    var values = Math.Max(CountMaxArguments(skill.Triggers), CountMaxArguments(skill.Actions));

                    if (values <= 1 || values == promoteLevels)
                        continue;

                    warnings.Add($"Лист Characters, персонаж {character.Id}: в promote_to_skill_levels {promoteLevels} значений, а у скилла {skillId} по {values} значений на параметр. По ГДД количество должно совпадать.");
                }
            }
        }

        private int CountMaxArguments(string text)
        {
            var max = 0;
            var open = -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    open = i;

                    continue;
                }

                if (text[i] != '}' || open < 0)
                    continue;

                var count = 0;
                var hasValue = false;

                for (int j = open + 1; j < i; j++)
                {
                    var symbol = text[j];

                    if (symbol == ':' || symbol == ',')
                    {
                        if (hasValue)
                            count += 1;

                        hasValue = false;

                        continue;
                    }

                    if (char.IsWhiteSpace(symbol) == false)
                        hasValue = true;
                }

                if (hasValue)
                    count += 1;

                if (max < count)
                    max = count;

                open = -1;
            }

            return max;
        }

        private void CollectStoryReferences(IConfigDistributor distributor, List<string> warnings)
        {
            var levels = distributor.StoryLevels.Collection;

            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];

                var missingStages = new List<int>();

                for (int stageIndex = 0; stageIndex < level.StageIds.Length; stageIndex++)
                {
                    if (distributor.StoryStages.TryGet(level.StageIds[stageIndex], out _) == false)
                        missingStages.Add(level.StageIds[stageIndex]);
                }

                if (0 < missingStages.Count)
                    warnings.Add($"Лист Story_levels, уровень {level.Id}: этапов {string.Join(", ", missingStages)} нет в Story_stages, уровень не запустится.");

                for (int triggerIndex = 0; triggerIndex < level.TriggerTypes.Length; triggerIndex++)
                {
                    var triggerType = level.TriggerTypes[triggerIndex];

                    if (triggerType == StoryLevelTriggerType.Unknown)
                    {
                        warnings.Add($"Лист Story_levels, уровень {level.Id}: неизвестный тип условия открытия {triggerIndex + 1} в trigger_types, сервер считает это условие выполненным.");

                        continue;
                    }

                    if (triggerType != StoryLevelTriggerType.StoryLevelCompleted && triggerType != StoryLevelTriggerType.StoryLevelNotCompleted)
                        continue;

                    if (level.TriggerValues.Length <= triggerIndex)
                    {
                        warnings.Add($"Лист Story_levels, уровень {level.Id}: у условия открытия {triggerIndex + 1} нет значения в trigger_values.");

                        continue;
                    }

                    var requiredLevelId = level.TriggerValues[triggerIndex];

                    if (distributor.StoryLevels.TryGet(requiredLevelId, out _) == false)
                        warnings.Add($"Лист Story_levels, уровень {level.Id}: условие открытия ссылается на уровень {requiredLevelId}, которого нет в листе.");
                }
            }

            var stages = distributor.StoryStages.Collection;

            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];

                if (stage.EventIds.Length == 0)
                    continue;

                for (int eventIndex = 0; eventIndex < stage.EventIds.Length; eventIndex++)
                {
                    if (distributor.StoryEvents.TryGet(stage.EventIds[eventIndex], out _) == false)
                        warnings.Add($"Лист Story_stages, этап {stage.Id}: события {stage.EventIds[eventIndex]} нет в Story_events.");
                }

                if (stage.EventChances.Length != 0 && stage.EventChances.Length != stage.EventIds.Length)
                    warnings.Add($"Лист Story_stages, этап {stage.Id}: событий {stage.EventIds.Length}, а весов в event_chances {stage.EventChances.Length}. Недостающие веса считаются 1.");
            }
        }

        private void CollectEquipmentMergeChains(IConfigDistributor distributor, List<string> warnings)
        {
            var equipments = distributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var source = equipments[i];

                if (string.IsNullOrWhiteSpace(source.MergeRequirements))
                    continue;

                IEquipmentMapper? target = null;
                var targets = 0;

                for (int candidateIndex = 0; candidateIndex < equipments.Count; candidateIndex++)
                {
                    var candidate = equipments[candidateIndex];

                    if (candidate.MergeGroup != source.MergeGroup || candidate.MergeNumber != source.MergeNumber + 1)
                        continue;

                    target = candidate;
                    targets += 1;
                }

                if (target == null)
                {
                    warnings.Add($"Лист Equipments, предмет {source.Id}: заданы merge_requirements, но в merge_group {source.MergeGroup} нет предмета с merge_number {source.MergeNumber + 1}, трансформация не сработает.");

                    continue;
                }

                if (1 < targets)
                    warnings.Add($"Лист Equipments, предмет {source.Id}: в merge_group {source.MergeGroup} несколько предметов с merge_number {source.MergeNumber + 1}, трансформация возьмёт первый ({target.Id}).");

                if (target.Type != source.Type)
                    warnings.Add($"Лист Equipments, предмет {source.Id}: трансформируется в предмет {target.Id} другого вида ({source.Type} → {target.Type}).");

                var sourceMaxLevel = distributor.EquipmentPromotes.GetMaxLevel(source.PromoteId);
                var targetMaxLevel = distributor.EquipmentPromotes.GetMaxLevel(target.PromoteId);

                if (targetMaxLevel < sourceMaxLevel)
                    warnings.Add($"Лист Equipments, предмет {source.Id}: максимальный уровень {sourceMaxLevel}, а у предмета трансформации {target.Id} только {targetMaxLevel}. Уровень после трансформации будет ограничен {targetMaxLevel}.");
            }
        }

        private void CollectStartPerkGroups(IConfigDistributor distributor, List<string> warnings)
        {
            if (distributor.Constants.TryGet(StartPerkGroupsConstant, out var constant) == false || string.IsNullOrWhiteSpace(constant.ConstantValue))
                return;

            var parts = constant.ConstantValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupId) == false)
                {
                    warnings.Add($"Лист Constants, {StartPerkGroupsConstant}: «{part}» не число, этот стартовый выбор перка пропустится.");

                    continue;
                }

                if (distributor.PerkGroups.TryGet(groupId, out _) == false)
                    warnings.Add($"Лист Constants, {StartPerkGroupsConstant}: группы перков {groupId} нет в листе Perk_groups, этот стартовый выбор перка пропустится.");
            }
        }

        private void CollectSummonDamageLevels(IConfigDistributor distributor, List<string> warnings)
        {
            var summons = distributor.Summons.Collection;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];
                var maxLevel = ReadMaxSummonLevel(distributor, summon.LevelPatternId);

                if (maxLevel <= summon.DamageOnLevels.Length)
                    continue;

                warnings.Add($"Лист Summons, саммон {summon.Id}: в dmg_on_lvls {summon.DamageOnLevels.Length} значений, а уровней в Summon_levels до {maxLevel}. На уровнях выше {summon.DamageOnLevels.Length} урон перестанет расти.");
            }
        }

        private int ReadMaxSummonLevel(IConfigDistributor distributor, int patternId)
        {
            var levels = distributor.SummonLevels.Collection;
            var maxLevel = 1;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].PatternId == patternId && maxLevel < levels[i].Level)
                    maxLevel = levels[i].Level;
            }

            return maxLevel;
        }

        private void CollectEquipmentBonusLevels(IConfigDistributor distributor, List<string> warnings)
        {
            var equipments = distributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var equipment = equipments[i];

                if (equipment.PromoteId <= 0)
                    continue;

                var maxLevel = distributor.EquipmentPromotes.GetMaxLevel(equipment.PromoteId);

                for (int bonusIndex = 0; bonusIndex < equipment.BonusIds.Length; bonusIndex++)
                {
                    var bonusId = equipment.BonusIds[bonusIndex];

                    if (distributor.Bonuses.TryGet(bonusId, out var bonus) == false)
                        continue;

                    var valuesCount = bonus.BonusValues.Length;

                    if (valuesCount == maxLevel)
                        continue;

                    if (valuesCount < maxLevel)
                    {
                        warnings.Add(valuesCount <= 1
                            ? $"Лист Bonuses, бонус {bonusId} снаряжения {equipment.Id}: одно значение, а уровней снаряжения до {maxLevel}. Бонус не будет расти с уровнем, по ГДД значений должно быть столько же, сколько уровней."
                            : $"Лист Bonuses, бонус {bonusId} снаряжения {equipment.Id}: значений {valuesCount}, а уровней снаряжения до {maxLevel}. На уровнях выше {valuesCount} бонус перестанет расти.");

                        continue;
                    }

                    warnings.Add($"Лист Bonuses, бонус {bonusId} снаряжения {equipment.Id}: значений {valuesCount}, а уровней снаряжения только {maxLevel}. Лишние значения не используются.");
                }
            }
        }
    }
}
