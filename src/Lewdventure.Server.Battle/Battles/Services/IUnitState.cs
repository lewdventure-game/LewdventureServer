namespace Server.Battles
{
    internal interface IUnitState
    {
        public int Id { get; }

        public int Level { get; }

        public ICharacteristicState CharacteristicState { get; }

        public CharacteristicBuckets BaseBuckets { get; }

        public List<ActiveBattleBonus> ActiveBonuses { get; }

        public IReadOnlyList<ISkill> Skills { get; }

        public List<IPerk> Perks { get; }

        public UnitFlags Flags { get; }

        public int SlotIndex { get; }

        public BattleSide Side { get; }

        public List<ActiveStatus> ActiveStatuses { get; }

        public IReadOnlyList<EquippedEntityRef> EquippedEntities { get; }

        public bool IsAlive();

        public bool CanUseNormalAttack(int currentTurn);

        public void RegisterNormalAttack(int currentTurn);

        public void RegisterEquippedEntity(string entityType, int entityId);

        public bool HasEquippedEntity(string entityType, int entityId);
    }
}
