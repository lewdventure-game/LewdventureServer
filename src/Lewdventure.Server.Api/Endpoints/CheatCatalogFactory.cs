using System.Globalization;
using Server.Bonuses;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class CheatCatalogFactory
    {
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly ProgressionLimits _progressionLimits;
        private readonly ResourceKeyCollector _resourceKeyCollector;
        private readonly RewardApplier _rewardApplier;

        public CheatCatalogFactory(
            IBonusWorkModeParser bonusWorkModeParser,
            ProgressionLimits progressionLimits,
            ResourceKeyCollector resourceKeyCollector,
            RewardApplier rewardApplier)
        {
            _bonusWorkModeParser = bonusWorkModeParser;
            _progressionLimits = progressionLimits;
            _resourceKeyCollector = resourceKeyCollector;
            _rewardApplier = rewardApplier;
        }

        public CheatCatalogResponse Create(string configVersion, IConfigDistributor configDistributor)
        {
            var response = new CheatCatalogResponse
            {
                ConfigVersion = configVersion,
                Resources = _resourceKeyCollector.Collect(configDistributor),
            };

            AddCharacters(configDistributor, response.Characters);
            AddSummons(configDistributor, response.Summons);
            AddEquipment(configDistributor, response.Equipment);
            AddBonuses(configDistributor, response.Bonuses);
            AddStoryLevels(configDistributor, response.StoryLevels);

            return response;
        }

        private void AddCharacters(IConfigDistributor configDistributor, List<CheatCatalogItem> items)
        {
            var characters = configDistributor.Characters.Collection;

            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];

                items.Add(new CheatCatalogItem
                {
                    Id = character.Id,
                    Name = character.ArtName,
                    Details = character.IsMelee ? "melee" : "ranged",
                    MaxLevel = _progressionLimits.GetMaxPromoteLevel(character, configDistributor),
                });
            }
        }

        private void AddSummons(IConfigDistributor configDistributor, List<CheatCatalogItem> items)
        {
            var summons = configDistributor.Summons.Collection;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];

                items.Add(new CheatCatalogItem
                {
                    Id = summon.Id,
                    Name = summon.ArtName,
                    Details = summon.Rarity.ToString(),
                    MaxLevel = _progressionLimits.GetMaxSummonLevel(summon, configDistributor),
                    MaxMastery = _progressionLimits.GetMaxMasteryLevel(summon, configDistributor),
                });
            }
        }

        private void AddEquipment(IConfigDistributor configDistributor, List<CheatCatalogItem> items)
        {
            var equipments = configDistributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var equipment = equipments[i];

                items.Add(new CheatCatalogItem
                {
                    Id = equipment.Id,
                    Name = equipment.ArtName,
                    Details = equipment.Type + " " + equipment.Rarity,
                    MaxLevel = _progressionLimits.GetMaxEquipmentLevel(equipment, configDistributor),
                });
            }
        }

        private void AddBonuses(IConfigDistributor configDistributor, List<CheatCatalogItem> items)
        {
            foreach (var bonus in configDistributor.Bonuses.Values)
            {
                if (_bonusWorkModeParser.TryParse(bonus.WorkModeParameters, out var workMode) == false || _rewardApplier.IsAccountScoped(workMode) == false)
                    continue;

                items.Add(new CheatCatalogItem
                {
                    Id = bonus.Id,
                    Name = bonus.BonusType.ToString(),
                    Details = bonus.OperatorType + " " + bonus.BonusValue.ToString(CultureInfo.InvariantCulture) + " " + bonus.WorkModeParameters,
                });
            }

            items.Sort(CompareById);
        }

        private void AddStoryLevels(IConfigDistributor configDistributor, List<int> storyLevels)
        {
            var levels = configDistributor.StoryLevels.Collection;

            for (int i = 0; i < levels.Count; i++)
                storyLevels.Add(levels[i].Id);

            storyLevels.Sort();
        }

        private int CompareById(CheatCatalogItem left, CheatCatalogItem right)
        {
            return left.Id.CompareTo(right.Id);
        }
    }
}
