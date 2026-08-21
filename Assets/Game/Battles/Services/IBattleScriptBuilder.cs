namespace Server.Battles
{
    internal interface IBattleScriptBuilder
    {
        public void Add(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            IUnitState actor,
            List<BattleCommand> commands,
            IUnitState target);

        public void Add(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            IUnitState actor,
            List<BattleCommand> commands);

        public void Add(
            List<BattleStep> steps,
            int turn,
            BattlePhaseType phase,
            List<BattleCommand> commands);
    }
}
