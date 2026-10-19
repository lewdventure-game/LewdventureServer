using Server.Infrastructure.Mongo.Players;

namespace Server.Balance
{
    internal sealed class BalanceSetupReader
    {
        public BalanceSetup Read(PlayerProfileDocument profile)
        {
            var setup = new BalanceSetup { CharacterId = profile.Loadout.CharacterId, PromoteLevel = 1 };

            for (int i = 0; i < profile.Characters.Count; i++)
            {
                if (profile.Characters[i].ConfigId == setup.CharacterId)
                    setup.PromoteLevel = Math.Max(1, profile.Characters[i].PromoteLevel);
            }

            for (int i = 0; i < profile.Loadout.Summons.Count; i++)
            {
                for (int summonIndex = 0; summonIndex < profile.Summons.Count; summonIndex++)
                {
                    var summon = profile.Summons[summonIndex];

                    if (summon.ConfigId == profile.Loadout.Summons[i])
                        setup.Summons.Add(new BalanceSummonSetup { Id = summon.ConfigId, Level = summon.Level, MasteryLevel = summon.MasteryLevel, SkillLevels = new List<int>(summon.SkillLevels) });
                }
            }

            foreach (var pair in profile.Loadout.Equipment)
            {
                for (int i = 0; i < profile.Equipment.Count; i++)
                {
                    var item = profile.Equipment[i];

                    if (string.Equals(item.InstanceId, pair.Value, StringComparison.Ordinal))
                        setup.Equipment.Add(new BalanceEquipmentSetup { ConfigId = item.ConfigId, Level = item.Level });
                }
            }

            for (int i = 0; i < profile.Bonuses.Count; i++)
                setup.AccountBonuses.Add(new BalanceBonusSetup { Id = profile.Bonuses[i].BonusId, Count = profile.Bonuses[i].Count });

            return setup;
        }
    }
}
