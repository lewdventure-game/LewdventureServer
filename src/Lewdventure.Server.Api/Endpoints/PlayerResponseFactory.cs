using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class PlayerResponseFactory
    {
        private readonly DialogueLaunchKeys _dialogueLaunchKeys = new();

        public PlayerProfileResponse Create(PlayerProfileDocument profile, IConfigDistributor configDistributor)
        {
            var response = Create(profile);

            response.PendingDialogues.Clear();

            for (int i = 0; i < profile.Dialogues.Pending.Count; i++)
            {
                var launch = profile.Dialogues.Pending[i];

                if (configDistributor.Dialogues.Contains(launch.DialogueId))
                    response.PendingDialogues.Add(CreateLaunch(launch));
            }

            return response;
        }

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

            for (int i = 0; i < profile.Dialogues.Pending.Count; i++)
            {
                var launch = profile.Dialogues.Pending[i];

                response.PendingDialogues.Add(CreateLaunch(launch));
            }

            for (int i = 0; i < profile.Story.Records.Count; i++)
            {
                var record = profile.Story.Records[i];

                response.Story.Records.Add(new PlayerStoryRecordResponse
                {
                    StoryLevelId = record.StoryLevelId,
                    BestStageIndex = record.BestStageIndex,
                });
            }

            for (int i = 0; i < profile.Characters.Count; i++)
            {
                var character = profile.Characters[i];

                response.Characters.Add(new PlayerCharacterResponse
                {
                    Id = character.ConfigId,
                    Copies = character.Copies,
                    PromoteLevel = character.PromoteLevel,
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
                    SkillLevels = new List<int>(summon.SkillLevels),
                    UnlockedScenes = new List<int>(summon.UnlockedSceneIds),
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

        private DialogueLaunchResponse CreateLaunch(PlayerDialogueLaunchDocument launch)
        {
            return new DialogueLaunchResponse
            {
                LaunchKey = launch.LaunchKey,
                DialogueId = launch.DialogueId,
                Trigger = _dialogueLaunchKeys.ResolveTrigger(launch.LaunchKey),
            };
        }
    }
}
