namespace Server.Battles
{
    internal sealed class BattleScriptBuilder : IBattleScriptBuilder
    {
        private readonly ILogger<BattleScriptBuilder> _logger;

        public BattleScriptBuilder(ILogger<BattleScriptBuilder> logger)
        {
            _logger = logger;
        }

        public void Add(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            IUnitState actor,
            List<BattleCommand> commands,
            IUnitState target)
        {
            AddInternal(
                steps,
                turn,
                phase,
                actor.Id,
                actor.SlotIndex,
                commands,
                target.Id,
                target.SlotIndex);
        }

        public void Add(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            IUnitState actor,
            List<BattleCommand> commands)
        {
            AddInternal(
                steps,
                turn,
                phase,
                actor.Id,
                actor.SlotIndex,
                commands,
                -1,
                -1);
        }

        public void Add(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            List<BattleCommand> commands)
        {
            AddInternal(
                steps,
                turn,
                phase,
                -1,
                -1,
                commands,
                -1,
                -1);
        }

        private void AddInternal(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            int actorId,
            int actorSlotIndex,
            List<BattleCommand> commands,
            int targetId,
            int targetSlotIndex)
        {
            var index = steps.Count;

            steps.Add(new BattleStep
            {
                Index = index,
                Turn = turn,
                Phase = phase,
                ActorId = actorId,
                ActorSlotIndex = actorSlotIndex,
                TargetId = targetId,
                TargetSlotIndex = targetSlotIndex,
                Commands = commands,
            });

            _logger.LogDebug($"[Story][Battle]: Step index = {index}, turn = {turn}, phase = {phase}, actorId = {actorId}, actorSlotIndex = {actorSlotIndex}, targetId = {targetId}, targetSlotIndex = {targetSlotIndex}, commands = {commands.Count}");
        }
    }
}
