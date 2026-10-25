namespace Server.Battles
{
    public interface IBattleFormulaRecorder
    {
        public bool IsEnabled { get; }

        public void Record(BattleFormula formula);
    }
}
