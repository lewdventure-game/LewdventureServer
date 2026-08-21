using Newtonsoft.Json.Linq;

namespace Server.Battles
{
    internal sealed class BattleCommandFactory : IBattleCommandFactory
    {
        public BattleCommand Wait(float seconds)
        {
            return new BattleCommand
            {
                CommandType = CommandType.Wait,
                Parameters = new JObject
                {
                    ["seconds"] = seconds,
                },
            };
        }

        public BattleCommand Approach(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, bool isMelee)
        {
            return new BattleCommand
            {
                CommandType = CommandType.Approach,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["isMelee"] = isMelee,
                },
            };
        }

        public BattleCommand ReturnToPosition(int actorId, int actorSlotIndex)
        {
            return new BattleCommand
            {
                CommandType = CommandType.ReturnToPosition,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                },
            };
        }

        public BattleCommand PlayAnimation(int actorId, int actorSlotIndex, string animationKey)
        {
            return new BattleCommand
            {
                CommandType = CommandType.PlayAnimation,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["animationKey"] = animationKey,
                },
            };
        }

        public BattleCommand ShowDamage(
            int actorId,
            int actorSlotIndex,
            int targetId,
            int targetSlotIndex,
            float damage,
            bool isCritical,
            bool isEvaded)
        {
            return new BattleCommand
            {
                CommandType = CommandType.ShowDamage,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["damage"] = damage,
                    ["isCritical"] = isCritical,
                    ["isEvaded"] = isEvaded,
                },
            };
        }

        public BattleCommand ShowHeal(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, float heal)
        {
            return new BattleCommand
            {
                CommandType = CommandType.ShowHeal,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["heal"] = heal,
                },
            };
        }

        public BattleCommand ShowMiss(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex)
        {
            return new BattleCommand
            {
                CommandType = CommandType.ShowMiss,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                },
            };
        }

        public BattleCommand ApplyStatus(
            int actorId,
            int actorSlotIndex,
            int targetId,
            int targetSlotIndex,
            int statusId,
            int stacks,
            int durationTurns)
        {
            var presentationDurationTurns = durationTurns;

            if (presentationDurationTurns == int.MaxValue)
                presentationDurationTurns = -1;

            return new BattleCommand
            {
                CommandType = CommandType.ApplyStatus,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["statusId"] = statusId,
                    ["stacks"] = stacks,
                    ["durationTurns"] = presentationDurationTurns,
                },
            };
        }

        public BattleCommand RemoveStatus(int targetId, int targetSlotIndex, int statusId)
        {
            return new BattleCommand
            {
                CommandType = CommandType.RemoveStatus,
                Parameters = new JObject
                {
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["statusId"] = statusId,
                },
            };
        }

        public BattleCommand TickStatus(int targetId, int targetSlotIndex, int statusId, float value)
        {
            return new BattleCommand
            {
                CommandType = CommandType.TickStatus,
                Parameters = new JObject
                {
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["statusId"] = statusId,
                    ["value"] = value,
                },
            };
        }

        public BattleCommand CastSkill(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, string skillId)
        {
            return new BattleCommand
            {
                CommandType = CommandType.CastSkill,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["skillId"] = skillId,
                },
            };
        }

        public BattleCommand TriggerPerk(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, int perkId)
        {
            return new BattleCommand
            {
                CommandType = CommandType.TriggerPerk,
                Parameters = new JObject
                {
                    ["actorId"] = actorId,
                    ["actorSlotIndex"] = actorSlotIndex,
                    ["targetId"] = targetId,
                    ["targetSlotIndex"] = targetSlotIndex,
                    ["perkId"] = perkId,
                },
            };
        }

        public BattleCommand SetHp(int unitId, int slotIndex, float hp)
        {
            return new BattleCommand
            {
                CommandType = CommandType.SetHp,
                Parameters = new JObject
                {
                    ["unitId"] = unitId,
                    ["slotIndex"] = slotIndex,
                    ["hp"] = hp,
                },
            };
        }

        public BattleCommand SetEnergy(int unitId, int slotIndex, float energy)
        {
            return new BattleCommand
            {
                CommandType = CommandType.SetEnergy,
                Parameters = new JObject
                {
                    ["unitId"] = unitId,
                    ["slotIndex"] = slotIndex,
                    ["energy"] = energy,
                },
            };
        }

        public BattleCommand SetBonus(int unitId, int slotIndex, int bonusId, float value, int sourceId)
        {
            return new BattleCommand
            {
                CommandType = CommandType.SetBonus,
                Parameters = new JObject
                {
                    ["unitId"] = unitId,
                    ["slotIndex"] = slotIndex,
                    ["bonusId"] = bonusId,
                    ["value"] = value,
                    ["sourceId"] = sourceId,
                },
            };
        }

        public BattleCommand SpawnUnit(int unitId, int slotIndex)
        {
            return new BattleCommand
            {
                CommandType = CommandType.SpawnUnit,
                Parameters = new JObject
                {
                    ["unitId"] = unitId,
                    ["slotIndex"] = slotIndex,
                },
            };
        }

        public BattleCommand DespawnUnit(int unitId, int slotIndex)
        {
            return new BattleCommand
            {
                CommandType = CommandType.DespawnUnit,
                Parameters = new JObject
                {
                    ["unitId"] = unitId,
                    ["slotIndex"] = slotIndex,
                },
            };
        }

        public BattleCommand KillUnit(int unitId, int slotIndex)
        {
            return new BattleCommand
            {
                CommandType = CommandType.KillUnit,
                Parameters = new JObject
                {
                    ["unitId"] = unitId,
                    ["slotIndex"] = slotIndex,
                },
            };
        }

        public BattleCommand GrantReward(string rewardType, int rewardId, int count, int targetId)
        {
            return new BattleCommand
            {
                CommandType = CommandType.GrantReward,
                Parameters = new JObject
                {
                    ["rewardType"] = rewardType,
                    ["rewardId"] = rewardId,
                    ["count"] = count,
                    ["targetId"] = targetId,
                },
            };
        }

        public BattleCommand GrantReward(string rewardType, string rewardKey, int count, int targetId)
        {
            return new BattleCommand
            {
                CommandType = CommandType.GrantReward,
                Parameters = new JObject
                {
                    ["rewardType"] = rewardType,
                    ["rewardId"] = rewardKey,
                    ["count"] = count,
                    ["targetId"] = targetId,
                },
            };
        }

        public BattleCommand SetBattleResult(OutcomeType outcome)
        {
            return new BattleCommand
            {
                CommandType = CommandType.SetBattleResult,
                Parameters = new JObject
                {
                    ["outcome"] = (int)outcome,
                },
            };
        }
    }
}
