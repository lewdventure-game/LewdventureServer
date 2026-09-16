using Newtonsoft.Json;
using Server.Services;

namespace Tests.Golden.Infrastructure
{
    internal sealed class ConfigDistributorDump
    {
        public Dictionary<string, string> Create(IConfigDistributor distributor)
        {
            return new Dictionary<string, string>
            {
                ["Constants"] = JsonConvert.SerializeObject(distributor.Constants.Collection),
                ["Artifacts"] = JsonConvert.SerializeObject(distributor.Artifacts.Collection),
                ["Aspects"] = JsonConvert.SerializeObject(distributor.Aspects.Collection),
                ["Bonuses"] = JsonConvert.SerializeObject(distributor.Bonuses.Values),
                ["Characters"] = JsonConvert.SerializeObject(distributor.Characters.Collection),
                ["Enemies"] = JsonConvert.SerializeObject(distributor.Enemies.Collection),
                ["Equipments"] = JsonConvert.SerializeObject(distributor.Equipments.Collection),
                ["ExperienceLevelPatterns"] = JsonConvert.SerializeObject(distributor.ExperienceLevelPatterns.Collection),
                ["Masteries"] = JsonConvert.SerializeObject(distributor.Masteries.Collection),
                ["PerkGroups"] = JsonConvert.SerializeObject(distributor.PerkGroups.Collection),
                ["Perks"] = JsonConvert.SerializeObject(distributor.Perks.Collection),
                ["Statuses"] = JsonConvert.SerializeObject(distributor.Statuses.Collection),
                ["StoryEvents"] = JsonConvert.SerializeObject(distributor.StoryEvents.Collection),
                ["StoryLevels"] = JsonConvert.SerializeObject(distributor.StoryLevels.Collection),
                ["StoryStages"] = JsonConvert.SerializeObject(distributor.StoryStages.Collection),
                ["SummonLevels"] = JsonConvert.SerializeObject(distributor.SummonLevels.Collection),
                ["Summons"] = JsonConvert.SerializeObject(distributor.Summons.Collection),
                ["Trainings"] = JsonConvert.SerializeObject(distributor.Trainings.Collection),
            };
        }
    }
}
