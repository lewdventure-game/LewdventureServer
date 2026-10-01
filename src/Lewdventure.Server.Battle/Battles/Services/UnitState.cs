namespace Server.Battles
{
    internal sealed class UnitState : IUnitState
    {
        private readonly int _id;
        private readonly int _level;
        private readonly ICharacteristicState _characteristicState;
        private readonly CharacteristicBuckets _baseBuckets;
        private readonly List<ActiveBattleBonus> _activeBonuses = new();
        private readonly IReadOnlyList<ISkill> _skills;
        private readonly List<IPerk> _perks;
        private readonly UnitFlags _flags;
        private readonly int _attackCooldownTurns;
        private readonly int _slotIndex;
        private readonly BattleSide _side;
        private readonly List<ActiveStatus> _activeStatuses = new();
        private readonly List<EquippedEntityRef> _equippedEntities = new();
        private readonly List<string> _equipmentSkillKeys = new();
        private readonly List<SkillRuntimeState> _skillStates = new();
        private int _skillLevel;
        private int _nextAttackTurn;

        public int Id => _id;

        public int Level => _level;

        public ICharacteristicState CharacteristicState => _characteristicState;

        public CharacteristicBuckets BaseBuckets => _baseBuckets;

        public List<ActiveBattleBonus> ActiveBonuses => _activeBonuses;

        public IReadOnlyList<ISkill> Skills => _skills;

        public List<IPerk> Perks => _perks;

        public UnitFlags Flags => _flags;

        public int SlotIndex => _slotIndex;

        public BattleSide Side => _side;

        public List<ActiveStatus> ActiveStatuses => _activeStatuses;

        public IReadOnlyList<EquippedEntityRef> EquippedEntities => _equippedEntities;

        public IReadOnlyList<string> EquipmentSkillKeys => _equipmentSkillKeys;

        public int SkillLevel => _skillLevel;

        public UnitState(
            int id,
            int level,
            ICharacteristicState characteristicState,
            CharacteristicBuckets baseBuckets,
            IReadOnlyList<ISkill> skills,
            List<IPerk> perks,
            UnitFlags flags,
            int attackCooldownTurns,
            int slotIndex,
            BattleSide side)
        {
            _id = id;
            _level = level;
            _characteristicState = characteristicState;
            _baseBuckets = baseBuckets;
            _skills = skills;
            _perks = perks;
            _flags = flags;
            _attackCooldownTurns = attackCooldownTurns;
            _slotIndex = slotIndex;
            _side = side;
        }

        public bool IsAlive()
        {
            return 0f < _characteristicState.Health;
        }

        public bool CanUseNormalAttack(int currentTurn)
        {
            if (currentTurn < _nextAttackTurn)
                return false;

            return true;
        }

        public void RegisterNormalAttack(int currentTurn)
        {
            _nextAttackTurn = currentTurn + _attackCooldownTurns + 1;
        }

        public void SetSkillLevel(int skillLevel)
        {
            _skillLevel = skillLevel < 0 ? 0 : skillLevel;
        }

        public SkillRuntimeState GetSkillState(int skillId)
        {
            for (int i = 0; i < _skillStates.Count; i++)
            {
                if (_skillStates[i].SkillId == skillId)
                    return _skillStates[i];
            }

            var state = new SkillRuntimeState(skillId);

            _skillStates.Add(state);

            return state;
        }

        public void RegisterEquipmentSkill(string skillKey)
        {
            if (string.IsNullOrWhiteSpace(skillKey))
                return;

            var trimmed = skillKey.Trim();

            for (int i = 0; i < _equipmentSkillKeys.Count; i++)
            {
                if (string.Equals(_equipmentSkillKeys[i], trimmed, StringComparison.Ordinal))
                    return;
            }

            _equipmentSkillKeys.Add(trimmed);
        }

        public void RegisterEquippedEntity(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                return;

            if (HasEquippedEntity(entityType, entityId))
                return;

            _equippedEntities.Add(new EquippedEntityRef(entityType.Trim(), entityId));
        }

        public bool HasEquippedEntity(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                return false;

            for (int i = 0; i < _equippedEntities.Count; i++)
            {
                var equipped = _equippedEntities[i];

                if (equipped.EntityId != entityId)
                    continue;

                if (string.Equals(equipped.EntityType, entityType, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                return true;
            }

            return false;
        }
    }
}
