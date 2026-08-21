namespace Server.Battles
{
    internal interface IBattleCommandFactory
    {
        public BattleCommand Wait(float seconds);

        public BattleCommand Approach(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, bool isMelee);

        public BattleCommand ReturnToPosition(int actorId, int actorSlotIndex);

        public BattleCommand PlayAnimation(int actorId, int actorSlotIndex, string animationKey);

        public BattleCommand ShowDamage(
            int actorId,
            int actorSlotIndex,
            int targetId,
            int targetSlotIndex,
            float damage,
            bool isCritical,
            bool isEvaded);

        public BattleCommand ShowHeal(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, float heal);

        public BattleCommand ShowMiss(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex);

        public BattleCommand ApplyStatus(
            int actorId,
            int actorSlotIndex,
            int targetId,
            int targetSlotIndex,
            int statusId,
            int stacks,
            int durationTurns);

        public BattleCommand RemoveStatus(int targetId, int targetSlotIndex, int statusId);

        public BattleCommand TickStatus(int targetId, int targetSlotIndex, int statusId, float value);

        public BattleCommand CastSkill(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, string skillId);

        public BattleCommand TriggerPerk(int actorId, int actorSlotIndex, int targetId, int targetSlotIndex, int perkId);

        public BattleCommand SetHp(int unitId, int slotIndex, float hp);

        public BattleCommand SetEnergy(int unitId, int slotIndex, float energy);

        public BattleCommand SetBonus(int unitId, int slotIndex, int bonusId, float value, int sourceId);

        public BattleCommand SpawnUnit(int unitId, int slotIndex);

        public BattleCommand DespawnUnit(int unitId, int slotIndex);

        public BattleCommand KillUnit(int unitId, int slotIndex);

        public BattleCommand GrantReward(string rewardType, int rewardId, int count, int targetId);

        public BattleCommand GrantReward(string rewardType, string rewardKey, int count, int targetId);

        public BattleCommand SetBattleResult(OutcomeType outcome);
    }
}
