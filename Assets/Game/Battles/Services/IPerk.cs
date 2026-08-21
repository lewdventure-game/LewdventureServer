using Server.Perks;

namespace Server.Battles
{
    internal interface IPerk
    {
        public int Id { get; }

        public PerkType PerkType { get; }

        public int TriggerOrder { get; }

        public void OnEquipped(IUnitState owner, List<BattleCommand> commands);

        public bool CanTrigger(int currentTurn);

        public void Trigger(IPerkExecutionContext context);

        public void NotifyAction(BattlePerkActionType actionType, IPerkExecutionContext context);

        public bool TryResurrectOnDeath(
            IUnitState owner,
            List<BattleStep> steps,
            int currentTurn,
            IBattleCommandFactory battleCommandFactory,
            IBattleScriptBuilder battleScriptBuilder);
    }
}
