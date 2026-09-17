using Server.Perks;

namespace Server.Battles
{
    internal abstract class BasePerk : IPerk
    {
        private readonly int _id;
        private readonly PerkType _perkType;
        private readonly int _triggerOrder;

        public int Id => _id;

        public PerkType PerkType => _perkType;

        public int TriggerOrder => _triggerOrder;

        protected BasePerk(IPerkMapper mapper)
        {
            _id = mapper.Id;
            _perkType = mapper.PerkType;
            _triggerOrder = mapper.TriggerOrder;
        }

        public virtual void OnEquipped(IUnitState owner, List<BattleCommand> commands)
        {
        }

        public virtual bool CanTrigger(int currentTurn)
        {
            return true;
        }

        public virtual void Trigger(IPerkExecutionContext context)
        {
        }

        public virtual void NotifyAction(BattlePerkActionType actionType, IPerkExecutionContext context)
        {
        }

        public virtual bool TryResurrectOnDeath(
            IUnitState owner,
            List<BattleStep> steps,
            int currentTurn,
            IBattleCommandFactory battleCommandFactory,
            IBattleScriptBuilder battleScriptBuilder)
        {
            return false;
        }
    }
}
