using Server.Infrastructure.Mongo.Players;

namespace Server.Api.Endpoints
{
    internal sealed class PlayerResponseFactory
    {
        public PlayerProfileResponse Create(PlayerProfileDocument profile)
        {
            var response = new PlayerProfileResponse
            {
                UserId = profile.Id,
                Rev = profile.Rev,
                Resources = new Dictionary<string, long>(profile.Resources),
                Flags = new Dictionary<string, int>(profile.Flags),
                Loadout = new PlayerLoadoutResponse
                {
                    CharacterId = profile.Loadout.CharacterId,
                    Equipment = new Dictionary<string, string>(profile.Loadout.Equipment),
                    Summons = new List<int>(profile.Loadout.Summons),
                },
                Story = new PlayerStoryResponse
                {
                    CompletedLevelIds = new List<int>(profile.Story.CompletedLevelIds),
                    CurrentRunId = profile.Story.CurrentRunId,
                },
            };

            for (int i = 0; i < profile.Characters.Count; i++)
            {
                var character = profile.Characters[i];

                response.Characters.Add(new PlayerCharacterResponse
                {
                    Id = character.ConfigId,
                    Copies = character.Copies,
                    UpgradesApplied = character.UpgradesApplied,
                    UnlockedScenes = new List<int>(character.UnlockedSceneIds),
                });
            }

            for (int i = 0; i < profile.Summons.Count; i++)
            {
                var summon = profile.Summons[i];

                response.Summons.Add(new PlayerSummonResponse
                {
                    Id = summon.ConfigId,
                    Copies = summon.Copies,
                    Level = summon.Level,
                    MasteryLevel = summon.MasteryLevel,
                });
            }

            for (int i = 0; i < profile.Equipment.Count; i++)
            {
                var equipment = profile.Equipment[i];

                response.Equipment.Add(new PlayerEquipmentResponse
                {
                    InstanceId = equipment.InstanceId,
                    ConfigId = equipment.ConfigId,
                    Level = equipment.Level,
                    MergeNumber = equipment.MergeNumber,
                });
            }

            for (int i = 0; i < profile.Bonuses.Count; i++)
            {
                var bonus = profile.Bonuses[i];

                response.Bonuses.Add(new PlayerBonusResponse
                {
                    Id = bonus.BonusId,
                    Count = bonus.Count,
                });
            }

            return response;
        }
    }
}
